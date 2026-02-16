PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS animes (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  mal_id INTEGER NOT NULL UNIQUE,
  title TEXT NOT NULL,
  title_jp TEXT,
  synopsis TEXT,
  image_url TEXT,
  episodes_total INTEGER,
  score REAL,
  year INTEGER,
  season TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS genres (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS anime_genres (
  anime_id INTEGER NOT NULL,
  genre_id INTEGER NOT NULL,
  PRIMARY KEY (anime_id, genre_id),
  FOREIGN KEY (anime_id) REFERENCES animes(id) ON DELETE CASCADE,
  FOREIGN KEY (genre_id) REFERENCES genres(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS user_anime (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  anime_id INTEGER NOT NULL UNIQUE,
  status TEXT NOT NULL CHECK (status IN ('QueroVer','Assistindo','Concluido','Pausado','Dropado')),
  current_episode INTEGER NOT NULL DEFAULT 0,
  personal_score REAL,
  notes TEXT,
  is_favorite INTEGER NOT NULL DEFAULT 0,
  started_at TEXT,
  finished_at TEXT,
  updated_at TEXT NOT NULL DEFAULT (datetime('now')),
  FOREIGN KEY (anime_id) REFERENCES animes(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_user_anime_status ON user_anime(status);
CREATE INDEX IF NOT EXISTS idx_animes_title ON animes(title);
