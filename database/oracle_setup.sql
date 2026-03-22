-- ============================================================
--  DeployFlow -- Oracle Database Setup Script
--  Run as SYSDBA:
--    sqlplus sys@localhost:1521/orclpdb as sysdba @oracle_setup.sql
-- ============================================================

-- ── 1. Create Application User ─────────────────────────────
-- Drop existing user (clean re-install)
BEGIN
  EXECUTE IMMEDIATE 'DROP USER deployflow CASCADE';
EXCEPTION
  WHEN OTHERS THEN
    IF SQLCODE != -1918 THEN RAISE; END IF; -- ignore "user does not exist"
END;
/

CREATE USER deployflow IDENTIFIED BY "DeployFlow_2024!"
  DEFAULT TABLESPACE USERS
  TEMPORARY TABLESPACE TEMP
  PROFILE DEFAULT
  ACCOUNT UNLOCK;
/

-- ── 2. Grant Privileges ─────────────────────────────────────
GRANT CONNECT, RESOURCE TO deployflow;
GRANT CREATE SESSION TO deployflow;
GRANT CREATE TABLE TO deployflow;
GRANT CREATE SEQUENCE TO deployflow;
GRANT CREATE PROCEDURE TO deployflow;
GRANT CREATE VIEW TO deployflow;
GRANT CREATE INDEX TO deployflow;
GRANT CREATE TRIGGER TO deployflow;
GRANT UNLIMITED TABLESPACE TO deployflow;

-- Required by Oracle EF Core for migrations history table
GRANT SELECT ON SYS.V_$SESSION TO deployflow;
/

COMMIT;
/

-- ── 3. Verification ──────────────────────────────────────────
SELECT 'User DEPLOYFLOW created successfully' AS STATUS FROM DUAL;
/

-- ============================================================
--  NEXT STEPS:
--  1. dotnet restore  (from backend/)
--  2. dotnet ef migrations add InitialOracleSchema (from backend/src/DeployFlow.Infrastructure/)
--  3. dotnet run  (app runs MigrateAsync and seeds data automatically)
-- ============================================================
EXIT;
