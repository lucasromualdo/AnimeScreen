# API Notes

- API alvo: Jikan (dados do MyAnimeList)
- Endpoints planejados:
  - Busca por título
  - Detalhes por `mal_id`
- Estratégia:
  - Persistir retorno da API em `animes`
  - Atualizar `updated_at` a cada sincronização
