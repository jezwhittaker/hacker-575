# OctoSupply API (C#)

ASP.NET Core (.NET 10) port of the OctoSupply API.

## Run

```bash
dotnet restore
dotnet run
```

Default API URL: `http://localhost:3000`

## Environment

- `PORT` (default: `3000`)
- `DB_FILE` (default: `./data/app.db`)
- `API_CORS_ORIGINS` (comma-separated override; optional)
- `PAYMENT_PROVIDER` (default: `simulated`; any other value fails fast at startup)
- `PAYMENT_API_KEY` (optional; reserved for real providers, never commit it)
- `PAYMENT_CURRENCY` (default: `USD`)
- `CHECKOUT_BRANCH_ID` (optional; branch that receives checkout orders, defaults to the lowest `branch_id`)

## Checkout

`POST /api/carts/{cartId}/checkout` with `customerName`, `email`, `cardNumber`, `expiry` (`MM/YY`) and `cvc`.

- Success: `201` with the confirmed order. The order, its order details and a payment record are created and the cart is emptied in one transaction.
- Declined or invalid payment: `402` (`PAYMENT_DECLINED`). Nothing is written and the cart is left intact.
- Empty cart: `400`. Unknown cart: `404`. Cart changed during payment: `409`.

Only the last four card digits are stored. The simulated provider declines `4000000000000002` (card declined) and `4000000000009995` (insufficient funds), approves any other Luhn-valid card (for example `4242424242424242`) and rejects expired cards.

## Database

On startup, the API applies SQL migrations from:

- `../database/migrations/*.sql`

and seeds from:

- `../database/seed/*.sql`

## OpenAPI

- OpenAPI JSON: `http://localhost:3000/api-docs.json`

## Tests

Run C# tests from the `src/` directory:

```bash
make test-cs
```
