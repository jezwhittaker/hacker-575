import Database from 'better-sqlite3';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { DatabaseConnection } from '../db/sqlite';
import { DatabaseError } from '../utils/errors';
import { OrderDetailsRepository } from './orderDetailsRepo';

describe('OrderDetailsRepository', () => {
  let db: Database.Database;
  let repository: OrderDetailsRepository;

  beforeEach(() => {
    db = new Database(':memory:');
    db.exec(`
      CREATE TABLE order_details (
        order_id INTEGER NOT NULL,
        quantity INTEGER NOT NULL,
        unit_price REAL NOT NULL
      )
    `);
    repository = new OrderDetailsRepository(new DatabaseConnection(db));
  });

  afterEach(() => {
    db.close();
  });

  it('calculates the total value for an order', async () => {
    db.prepare('INSERT INTO order_details (order_id, quantity, unit_price) VALUES (?, ?, ?)')
      .run(1, 2, 12.5);
    db.prepare('INSERT INTO order_details (order_id, quantity, unit_price) VALUES (?, ?, ?)')
      .run(1, 3, 4);
    db.prepare('INSERT INTO order_details (order_id, quantity, unit_price) VALUES (?, ?, ?)')
      .run(2, 1, 100);

    await expect(repository.getTotalValueByOrderId(1)).resolves.toBe(37);
  });

  it('returns zero when an order has no details', async () => {
    await expect(repository.getTotalValueByOrderId(1)).resolves.toBe(0);
  });

  it('converts database failures to a DatabaseError', async () => {
    db.exec('DROP TABLE order_details');

    await expect(repository.getTotalValueByOrderId(1)).rejects.toBeInstanceOf(DatabaseError);
  });
});
