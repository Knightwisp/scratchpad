CREATE INDEX IF NOT EXISTS IX_processed_transactions_idempotency_key 
    ON processed_transactions(idempotency_key);

CREATE INDEX IF NOT EXISTS IX_processed_transactions_account_id 
    ON processed_transactions(account_id);

CREATE INDEX IF NOT EXISTS IX_event_outbox_status 
    ON event_outbox(status);

CREATE INDEX IF NOT EXISTS IX_event_outbox_created_date 
    ON event_outbox(created_date);