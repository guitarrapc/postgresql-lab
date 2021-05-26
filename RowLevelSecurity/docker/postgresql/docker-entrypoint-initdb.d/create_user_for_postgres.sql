CREATE USER app_user PASSWORD 'app_password';
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO app_user;
CREATE USER migration_user PASSWORD 'migration_password';
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO app_user;
