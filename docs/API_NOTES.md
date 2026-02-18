# API Notes

- API alvo: Jikan (dados do MyAnimeList)
- Endpoints planejados:
  - Busca por titulo
  - Detalhes por `mal_id`
- Estrategia:
  - Persistir retorno da API em `animes`
  - Atualizar `updated_at` a cada sincronizacao
