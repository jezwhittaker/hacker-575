import { Link } from 'react-router-dom';
import { useCart } from '../../../context/CartContext';
import { useTheme } from '../../../context/ThemeContext';

const MAX_QUANTITY = 99;

const formatCurrency = (value: number) => `$${value.toFixed(2)}`;

export default function CartPage() {
  const { cart, isLoading, isUpdating, error, updateItem, removeItem } = useCart();
  const { darkMode } = useTheme();

  const heading = darkMode ? 'text-light' : 'text-gray-800';
  const muted = darkMode ? 'text-gray-400' : 'text-gray-600';
  const card = darkMode ? 'bg-gray-800 border-gray-700' : 'bg-white border-gray-200';
  const stepper = `w-8 h-8 flex items-center justify-center ${darkMode ? 'text-light' : 'text-gray-700'} hover:text-primary disabled:opacity-50 disabled:cursor-not-allowed`;

  const handleError = () => alert('Could not update your cart. Please try again.');

  return (
    <div
      className={`min-h-screen ${darkMode ? 'bg-dark' : 'bg-gray-100'} pt-20 pb-16 px-4 transition-colors duration-300`}
    >
      <div className="max-w-4xl mx-auto space-y-6">
        <h1 className={`text-3xl font-bold ${heading}`}>Your Cart</h1>

        {isLoading && (
          <p role="status" className={muted}>
            Loading your cart...
          </p>
        )}
        {!!error && (
          <p role="alert" className="text-red-500">
            Failed to load your cart
          </p>
        )}

        {cart && cart.items.length === 0 && (
          <div className={`${card} border rounded-lg p-10 text-center`} role="status">
            <p className={`${heading} text-lg font-medium`}>Your cart is empty</p>
            <Link to="/products" className="text-primary hover:text-accent mt-2 inline-block">
              Browse products
            </Link>
          </div>
        )}

        {cart && cart.items.length > 0 && (
          <>
            <ul className="space-y-4">
              {cart.items.map((item) => (
                <li
                  key={item.cartItemId}
                  className={`${card} border rounded-lg p-4 flex flex-wrap items-center gap-4`}
                >
                  {item.imgName && (
                    <img
                      src={`/${item.imgName}`}
                      alt={item.name}
                      width={64}
                      height={64}
                      className="h-16 w-16 object-contain"
                    />
                  )}
                  <div className="flex-grow min-w-[8rem]">
                    <h2 className={`font-semibold ${heading}`}>{item.name}</h2>
                    <p className={`text-sm ${muted}`}>
                      {formatCurrency(item.unitPrice * (1 - item.discount))} each
                    </p>
                  </div>
                  <div className="flex items-center space-x-2">
                    <button
                      className={stepper}
                      disabled={isUpdating || item.quantity <= 1}
                      onClick={() => updateItem(item.productId, item.quantity - 1).catch(handleError)}
                      aria-label={`Decrease quantity of ${item.name}`}
                    >
                      <span aria-hidden="true">-</span>
                    </button>
                    <span className={heading} aria-label={`Quantity of ${item.name}`}>
                      {item.quantity}
                    </span>
                    <button
                      className={stepper}
                      disabled={isUpdating || item.quantity >= MAX_QUANTITY}
                      onClick={() => updateItem(item.productId, item.quantity + 1).catch(handleError)}
                      aria-label={`Increase quantity of ${item.name}`}
                    >
                      <span aria-hidden="true">+</span>
                    </button>
                  </div>
                  <span className={`w-24 text-right font-semibold ${heading}`}>
                    {formatCurrency(item.lineTotal)}
                  </span>
                  <button
                    className="text-red-500 hover:text-red-400 disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-red-500 rounded"
                    disabled={isUpdating}
                    onClick={() => removeItem(item.productId).catch(handleError)}
                    aria-label={`Remove ${item.name} from cart`}
                  >
                    Remove
                  </button>
                </li>
              ))}
            </ul>

            <div className={`${card} border rounded-lg p-4 flex justify-between items-center`}>
              <span className={`text-lg ${heading}`}>
                Total ({cart.itemCount} {cart.itemCount === 1 ? 'item' : 'items'})
              </span>
              <span className="text-primary text-2xl font-bold" id="cart-total">
                {formatCurrency(cart.total)}
              </span>
            </div>

            <div className="flex justify-end">
              <Link
                to="/checkout"
                className="bg-primary hover:bg-accent text-white px-6 py-2 rounded-lg transition-colors"
              >
                Proceed to checkout
              </Link>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
