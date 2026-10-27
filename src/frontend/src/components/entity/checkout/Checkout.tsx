import { FormEvent, useState } from 'react';
import axios from 'axios';
import { Link } from 'react-router-dom';
import { api } from '../../../api/config';
import { useCart } from '../../../context/CartContext';
import { useTheme } from '../../../context/ThemeContext';

interface CheckoutOrderItem {
  productId: number;
  name: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

interface CheckoutResult {
  orderId: number;
  status: string;
  orderDate: string;
  items: CheckoutOrderItem[];
  total: number;
  currency: string;
  paymentReference: string;
  cardLast4: string;
}

interface CheckoutForm {
  customerName: string;
  email: string;
  cardNumber: string;
  expiry: string;
  cvc: string;
}

const formatCurrency = (value: number) => `$${value.toFixed(2)}`;

const getErrorMessage = (err: unknown): string => {
  if (axios.isAxiosError(err)) {
    const message = err.response?.data?.error?.message;
    if (typeof message === 'string') {
      return message;
    }
  }
  return 'We could not process your payment. Please try again.';
};

export default function CheckoutPage() {
  const { cart, isLoading, refreshCart } = useCart();
  const { darkMode } = useTheme();
  const [form, setForm] = useState<CheckoutForm>({
    customerName: '',
    email: '',
    cardNumber: '',
    expiry: '',
    cvc: '',
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<CheckoutResult | null>(null);

  const heading = darkMode ? 'text-light' : 'text-gray-800';
  const muted = darkMode ? 'text-gray-400' : 'text-gray-600';
  const card = darkMode ? 'bg-gray-800 border-gray-700' : 'bg-white border-gray-200';
  const input = `w-full px-3 py-2 rounded border ${
    darkMode ? 'bg-gray-700 border-gray-600 text-light' : 'bg-white border-gray-300 text-gray-800'
  } focus:outline-none focus:ring-2 focus:ring-primary`;

  const setField = (name: keyof CheckoutForm) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((prev) => ({ ...prev, [name]: e.target.value }));

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!cart) {
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const { data } = await axios.post<CheckoutResult>(
        `${api.baseURL}${api.endpoints.carts}/${cart.cartId}/checkout`,
        form,
      );
      setResult(data);
      setForm((prev) => ({ ...prev, cardNumber: '', expiry: '', cvc: '' }));
      await refreshCart();
    } catch (err) {
      // The API leaves the cart untouched on failure, so the form stays editable.
      setError(getErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  };

  const renderField = (
    id: keyof CheckoutForm,
    label: string,
    props: React.InputHTMLAttributes<HTMLInputElement> = {},
  ) => (
    <div>
      <label htmlFor={id} className={`block text-sm font-medium mb-1 ${heading}`}>
        {label}
      </label>
      <input
        id={id}
        name={id}
        required
        value={form[id]}
        onChange={setField(id)}
        className={input}
        {...props}
      />
    </div>
  );

  return (
    <div
      className={`min-h-screen ${darkMode ? 'bg-dark' : 'bg-gray-100'} pt-20 pb-16 px-4 transition-colors duration-300`}
    >
      <div className="max-w-2xl mx-auto space-y-6">
        <h1 className={`text-3xl font-bold ${heading}`}>Checkout</h1>

        {result && (
          <div className={`${card} border rounded-lg p-6 space-y-4`} role="status">
            <h2 className="text-primary text-2xl font-bold">Order confirmed</h2>
            <p className={muted}>
              Order #{result.orderId} ({result.status}). Payment reference{' '}
              <span className="font-mono">{result.paymentReference}</span>, card ending{' '}
              {result.cardLast4}.
            </p>
            <ul className={`divide-y ${darkMode ? 'divide-gray-700' : 'divide-gray-200'}`}>
              {result.items.map((item) => (
                <li key={item.productId} className={`py-2 flex justify-between ${heading}`}>
                  <span>
                    {item.name} × {item.quantity}
                  </span>
                  <span>{formatCurrency(item.lineTotal)}</span>
                </li>
              ))}
            </ul>
            <p className={`text-lg font-bold flex justify-between ${heading}`}>
              <span>Total paid</span>
              <span className="text-primary">{formatCurrency(result.total)}</span>
            </p>
            <Link to="/products" className="text-primary hover:text-accent inline-block">
              Continue shopping
            </Link>
          </div>
        )}

        {!result && isLoading && (
          <p role="status" className={muted}>
            Loading your cart...
          </p>
        )}

        {!result && cart && cart.items.length === 0 && (
          <div className={`${card} border rounded-lg p-10 text-center`} role="status">
            <p className={`${heading} text-lg font-medium`}>Your cart is empty</p>
            <Link to="/products" className="text-primary hover:text-accent mt-2 inline-block">
              Browse products
            </Link>
          </div>
        )}

        {!result && cart && cart.items.length > 0 && (
          <>
            <div className={`${card} border rounded-lg p-4 flex justify-between items-center`}>
              <span className={`text-lg ${heading}`}>
                Order total ({cart.itemCount} {cart.itemCount === 1 ? 'item' : 'items'})
              </span>
              <span className="text-primary text-2xl font-bold" id="checkout-total">
                {formatCurrency(cart.total)}
              </span>
            </div>

            <form onSubmit={handleSubmit} className={`${card} border rounded-lg p-6 space-y-4`}>
              {error && (
                <p role="alert" className="text-red-500 font-medium">
                  {error}
                </p>
              )}
              {renderField('customerName', 'Name on order', { autoComplete: 'name' })}
              {renderField('email', 'Email', { type: 'email', autoComplete: 'email' })}
              {renderField('cardNumber', 'Card number', {
                inputMode: 'numeric',
                autoComplete: 'cc-number',
                placeholder: '4242 4242 4242 4242',
              })}
              <div className="grid grid-cols-2 gap-4">
                {renderField('expiry', 'Expiry (MM/YY)', {
                  autoComplete: 'cc-exp',
                  placeholder: 'MM/YY',
                  maxLength: 5,
                })}
                {renderField('cvc', 'CVC', {
                  inputMode: 'numeric',
                  autoComplete: 'cc-csc',
                  maxLength: 4,
                })}
              </div>
              <div className="flex justify-between items-center">
                <Link to="/cart" className="text-primary hover:text-accent">
                  Back to cart
                </Link>
                <button
                  type="submit"
                  disabled={submitting}
                  className="bg-primary hover:bg-accent text-white px-6 py-2 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {submitting ? 'Processing...' : `Pay ${formatCurrency(cart.total)}`}
                </button>
              </div>
            </form>
          </>
        )}
      </div>
    </div>
  );
}
