import { createContext, useContext, ReactNode } from 'react';
import axios from 'axios';
import { useMutation, useQuery, useQueryClient } from 'react-query';
import { api } from '../api/config';

export interface CartItem {
  cartItemId: number;
  productId: number;
  name: string;
  imgName: string | null;
  unitPrice: number;
  discount: number;
  quantity: number;
  lineTotal: number;
}

export interface Cart {
  cartId: number;
  items: CartItem[];
  itemCount: number;
  total: number;
}

interface CartContextType {
  cart: Cart | undefined;
  isLoading: boolean;
  isUpdating: boolean;
  error: unknown;
  addItem: (productId: number, quantity: number) => Promise<Cart>;
  updateItem: (productId: number, quantity: number) => Promise<Cart>;
  removeItem: (productId: number) => Promise<Cart>;
  refreshCart: () => Promise<void>;
}

const CART_ID_KEY = 'cartId';
const CART_QUERY_KEY = 'cart';
const cartsUrl = `${api.baseURL}${api.endpoints.carts}`;

const createCart = async (): Promise<Cart> => {
  const { data } = await axios.post<Cart>(cartsUrl);
  localStorage.setItem(CART_ID_KEY, String(data.cartId));
  return data;
};

// The browser keeps only the cart id; the cart itself lives in the API database.
const loadCart = async (): Promise<Cart> => {
  const storedId = localStorage.getItem(CART_ID_KEY);
  if (!storedId) {
    return createCart();
  }

  try {
    const { data } = await axios.get<Cart>(`${cartsUrl}/${storedId}`);
    return data;
  } catch (err) {
    if (axios.isAxiosError(err) && err.response?.status === 404) {
      return createCart();
    }
    throw err;
  }
};

const CartContext = createContext<CartContextType | null>(null);

export function CartProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const { data: cart, isLoading, error } = useQuery(CART_QUERY_KEY, loadCart, {
    staleTime: Infinity,
    retry: 1,
  });

  const onSuccess = (updated: Cart): void => {
    queryClient.setQueryData(CART_QUERY_KEY, updated);
  };

  const addMutation = useMutation(
    async ({ productId, quantity }: { productId: number; quantity: number }) => {
      const { data } = await axios.post<Cart>(`${cartsUrl}/${cart!.cartId}/items`, {
        productId,
        quantity,
      });
      return data;
    },
    { onSuccess },
  );

  const updateMutation = useMutation(
    async ({ productId, quantity }: { productId: number; quantity: number }) => {
      const { data } = await axios.put<Cart>(`${cartsUrl}/${cart!.cartId}/items/${productId}`, {
        quantity,
      });
      return data;
    },
    { onSuccess },
  );

  const removeMutation = useMutation(
    async (productId: number) => {
      const { data } = await axios.delete<Cart>(`${cartsUrl}/${cart!.cartId}/items/${productId}`);
      return data;
    },
    { onSuccess },
  );

  const isUpdating = addMutation.isLoading || updateMutation.isLoading || removeMutation.isLoading;

  return (
    <CartContext.Provider
      value={{
        cart,
        isLoading,
        isUpdating,
        error,
        addItem: (productId, quantity) => addMutation.mutateAsync({ productId, quantity }),
        updateItem: (productId, quantity) => updateMutation.mutateAsync({ productId, quantity }),
        removeItem: (productId) => removeMutation.mutateAsync(productId),
        refreshCart: () => queryClient.invalidateQueries(CART_QUERY_KEY),
      }}
    >
      {children}
    </CartContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useCart() {
  const context = useContext(CartContext);
  if (!context) {
    throw new Error('useCart must be used within a CartProvider');
  }
  return context;
}
