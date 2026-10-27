BEGIN;
CREATE TABLE IF NOT EXISTS cricastudio.statistics_daily (
    day date NOT NULL,
    entity_type text NOT NULL CHECK (entity_type IN ('shop','supplier','article')),
    entity_id uuid NOT NULL,
    metric text NOT NULL CHECK (metric IN ('view','click','whatsapp')),
    source text NOT NULL CHECK (source IN ('home','listing','direct','article')),
    count bigint NOT NULL DEFAULT 0 CHECK (count >= 0),
    PRIMARY KEY (day,entity_type,entity_id,metric,source)
);
COMMIT;
