import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import axios from 'axios';
import CheckoutPage from './Checkout';

vi.mock('axios', async () => {
  const actual = await vi.importActual<typeof import('axios')>('axios');
  return { default: { ...actual.default, post: vi.fn(), isAxiosError: actual.default.isAxiosError } };
});

const refreshCart = vi.fn().mockResolvedValue(undefined);
let mockCart: unknown;

vi.mock('../../../context/CartContext', () => ({
  useCart: () => ({ cart: mockCart, isLoading: false, refreshCart }),
}));
vi.mock('../../../context/ThemeContext', () => ({
  useTheme: () => ({ darkMode: false }),
}));

const filledCart = {
  cartId: 7,
  itemCount: 3,
  total: 78.74,
  items: [
    {
      cartItemId: 1,
      productId: 1,
      name: 'Widget',
      imgName: null,
      unitPrice: 35,
      discount: 0.25,
      quantity: 3,
      lineTotal: 78.74,
    },
  ],
};

async function fillAndSubmit(card: string) {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Name on order'), 'Alice');
  await user.type(screen.getByLabelText('Email'), 'alice@example.com');
  await user.type(screen.getByLabelText('Card number'), card);
  await user.type(screen.getByLabelText(/Expiry/), '12/40');
  await user.type(screen.getByLabelText('CVC'), '123');
  await user.click(screen.getByRole('button', { name: /pay/i }));
}

const renderPage = () =>
  render(
    <MemoryRouter>
      <CheckoutPage />
    </MemoryRouter>,
  );

describe('CheckoutPage', () => {
  beforeEach(() => {
    mockCart = filledCart;
    vi.mocked(axios.post).mockReset();
    refreshCart.mockClear();
  });
  afterEach(cleanup);

  it('shows the cart total on the pay button', () => {
    renderPage();
    expect(screen.getByRole('button', { name: 'Pay $78.74' })).toBeTruthy();
  });

  it('shows an empty state when the cart has no items', () => {
    mockCart = { cartId: 7, itemCount: 0, total: 0, items: [] };
    renderPage();
    expect(screen.getByText('Your cart is empty')).toBeTruthy();
  });

  it('shows a confirmation and refreshes the cart after successful payment', async () => {
    vi.mocked(axios.post).mockResolvedValue({
      data: {
        orderId: 12,
        status: 'confirmed',
        orderDate: '2025-01-01',
        items: [{ productId: 1, name: 'Widget', quantity: 3, unitPrice: 26.25, lineTotal: 78.74 }],
        total: 78.74,
        currency: 'USD',
        paymentReference: 'sim_abc',
        cardLast4: '4242',
      },
    });
    renderPage();
    await fillAndSubmit('4242424242424242');

    expect(await screen.findByText('Order confirmed')).toBeTruthy();
    expect(screen.getByText(/Order #12/)).toBeTruthy();
    expect(vi.mocked(axios.post).mock.calls[0][0]).toContain('/api/carts/7/checkout');
    expect(refreshCart).toHaveBeenCalled();
  });

  it('shows the API error and keeps the form when payment is declined', async () => {
    vi.mocked(axios.post).mockRejectedValue({
      isAxiosError: true,
      response: { status: 402, data: { error: { code: 'PAYMENT_DECLINED', message: 'Your card was declined.' } } },
    });
    vi.spyOn(axios, 'isAxiosError').mockReturnValue(true);
    renderPage();
    await fillAndSubmit('4000000000000002');

    expect((await screen.findByRole('alert')).textContent).toBe('Your card was declined.');
    expect(refreshCart).not.toHaveBeenCalled();
    await waitFor(() => expect(screen.getByRole('button', { name: /pay/i })).toHaveProperty('disabled', false));
  });
});
