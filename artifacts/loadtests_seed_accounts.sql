SET app.tenant_id = '00000000-0000-0000-0000-000000000001';

INSERT INTO ledger.accounts ("Id", "TenantId", "Name", "Currency", "CreatedAt")
SELECT
  ('00000000-0000-0000-0000-' || lpad(gs::text, 12, '0'))::uuid,
  '00000000-0000-0000-0000-000000000001'::uuid,
  'Load Test Account ' || gs::text,
  'USD',
  NOW()
FROM generate_series(0, 999) AS gs
ON CONFLICT ("Id") DO NOTHING;
