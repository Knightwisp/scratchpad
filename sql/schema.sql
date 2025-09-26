CREATE TABLE IF NOT EXISTS accounts (
    account_id INTEGER PRIMARY KEY AUTOINCREMENT,
    balance REAL NOT NULL CHECK (balance >= 0),
    created_date TEXT DEFAULT (datetime('now')),
    updated_date TEXT DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS processed_transactions (
    processed_transaction_id INTEGER PRIMARY KEY AUTOINCREMENT,
    idempotency_key TEXT NOT NULL UNIQUE,
    account_id INTEGER NOT NULL,
    amount REAL NOT NULL,
    transaction_date TEXT DEFAULT (datetime('now')),
    FOREIGN KEY (account_id) REFERENCES accounts(account_id)
);

CREATE TABLE IF NOT EXISTS event_outbox (
    event_outbox_id INTEGER PRIMARY KEY AUTOINCREMENT,
    event_type TEXT NOT NULL,
    payload TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'PENDING',
    created_date TEXT DEFAULT (datetime('now')),
    processed_date TEXT NULL
);