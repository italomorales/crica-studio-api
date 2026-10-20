BEGIN;

ALTER TABLE cricastudio.affiliate_products
    ADD COLUMN IF NOT EXISTS last_store_price numeric(12,2) CHECK (last_store_price >= 0),
    ADD COLUMN IF NOT EXISTS link_status varchar(20) NOT NULL DEFAULT 'unchecked' CHECK (link_status IN ('unchecked','ok','broken')),
    ADD COLUMN IF NOT EXISTS stock_status varchar(20) NOT NULL DEFAULT 'unchecked' CHECK (stock_status IN ('unchecked','in_stock','out_of_stock'));

COMMIT;
