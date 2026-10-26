-- Share IT .Clara v0.6.0-RC1-R3
-- Perfil pessoal, avatar e metadados leves do usuário.
-- Fotos de perfil são pequenas e de baixo volume; evidências/screenshot continuam fora do PostgreSQL.

ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS telefone varchar(30);
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS cargo varchar(120);
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS foto_perfil bytea;
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS foto_perfil_mime varchar(80);
ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS perfil_atualizado_em_utc timestamptz;

DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_usuarios_foto_perfil_mime') THEN
    ALTER TABLE usuarios ADD CONSTRAINT ck_usuarios_foto_perfil_mime
      CHECK (foto_perfil_mime IS NULL OR foto_perfil_mime IN ('image/jpeg','image/png','image/webp'));
  END IF;
END $$;
