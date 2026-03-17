-- Установка паролей для пользователей (хеш: SHA256 + Base64)
-- admin@gmail.com -> admin123
-- 1227733@mtp.by -> barbariki

UPDATE Users SET PasswordHash = 'JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=' WHERE LOWER(Email) = 'admin@gmail.com';
UPDATE Users SET PasswordHash = 'MqLd3BnbuPYgC6i10QC6mghptehTxdP+pyuDEFT49oA=' WHERE LOWER(Email) = '1227733@mtp.by';
