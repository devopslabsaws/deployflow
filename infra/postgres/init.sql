-- DeployFlow PostgreSQL initialization
-- Runs once on first container start

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";  -- for fast full-text search on LIKE queries

-- Application-level role (non-superuser)
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'deployflow') THEN
    CREATE ROLE deployflow LOGIN PASSWORD 'deployflow_dev_password';
  END IF;
END
$$;

GRANT ALL PRIVILEGES ON DATABASE deployflow TO deployflow;
