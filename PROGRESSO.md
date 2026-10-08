# OiramRPG — Progresso

Plano original: `C:\Users\danie\.claude\plans\quero-criar-um-jogo-buzzing-barto.md`

## Status (2026-10-08)

### Fatia vertical (concluída)
Vale com timed hits, jobs (FFT), loot (Diablo), menus, Golem. Balanceada com o simulador.

### Mundo, cidade e dungeons (concluído)
- **Mapa-múndi** estilo Mario RPG (`WorldMap`): Vale → Vila Ventura → Mina → Cripta → Caverna. Locais liberados
  por progresso (vencer o chefe do local anterior). A saída do Vale só abre depois do Golem.
- **Vila Ventura** (`Town_Vila`): mercearia (consumíveis + Poção Grande), ferreiro (8 itens aleatórios no nível da party,
  renova a cada dungeon; também compra), apostador (item misterioso por tipo), pousada (descansa + **salva**),
  moradores com dicas e **baú escondido invisível** com uma **Relíquia** (nova raridade: 5 afixos no máximo, +3 níveis).
- **Dungeons procedurais** (`Dungeon`, montada em runtime): salas + corredores com desníveis, 2–4 andares, tochas,
  baús (10% Mímico), caixas, escada, fogueira antes do chefe, tesouro e portal de vitória. 3 dungeons temáticas com
  6 inimigos novos e 3 chefes novos. **Inimigos escalam com o nível da party** e com a dificuldade escolhida
  (Fácil/Normal/Difícil/Pesadelo — deslocamento proporcional ao nível, recompensas e achado mágico maiores).
- **Save/load** em JSON com ids estáveis; tela de **título** com Continuar/Novo jogo.
- Itens de nível alto (bases a partir do Nv 5) e tier extra de afixos (Nv 10–12).

### Balanceamento
- `Docs/balanceamento.md`: **27/27 metas** — Vale (16) + dungeons (11). Ex.: Normal/Médio conclui 99%,
  Difícil/Médio 76%, Pesadelo/Experiente 67% (Pesadelo/Médio 4%); ~1 nível por descida Normal; uma descida Normal
  paga ~2,3 itens do ferreiro; a dificuldade se mantém do Nv 4 ao Nv 12.
- `BalanceGuardTests` falha se alguma meta quebrar.

### Testes e verificação
- **69 testes**: 63 EditMode (regras, loot, jobs, gerador de dungeons, escala, save/load, lojas, guarda de balanceamento)
  + 6 PlayMode (batalha automática, mapa ↔ batalha, menu de pausa, título/mapa-múndi, cidade + baú escondido,
  **dungeon completa** até o portal de vitória).
- Tour de screenshots (`OiramRPG.exe -oiram-tour <pasta>`) passa por título, Vale, mapa-múndi, lojas, baú escondido e dungeon.
- Git inicializado, **nenhum commit ainda**.

## O que ainda não foi validado por uma pessoa

- **Playtest humano pendente** — roteiro em `Docs/playtest.md` (agora inclui cidade e dungeons).
- O simulador decide bem nos menus e limpa todas as salas; jogadores reais erram mais (jogo um pouco mais difícil).
- O simulador não troca de job nem usa Ladino/Aprendiz; essa progressão só tem testes unitários.
- Itens de consumo não podem ser usados fora de batalha (só a pousada/fogueira curam no mapa).

## Pendências conhecidas (pequenas)

- O build é *development*: atalhos F1–F6 e log de playtest ativos (`BuildOptions.Development` em `Editor/BuildScript.cs`).
- O andar atual da dungeon não é salvo (sair do jogo no meio de uma descida volta ao último save da pousada).
- Avisos de shaders de pós-processamento no log do player (renderer do template URP; inofensivos).

## Próximos marcos sugeridos

1. Playtest humano e calibração dos timed hits com os logs.
2. Usar consumíveis pelo menu de pausa; mais cidades (cada uma com seu baú escondido).
3. Mais dungeons/temas, chefes com mecânicas (fases, fraquezas), conjuntos ou lendários fixos se quiser.
4. Arte low-poly real, animações e áudio (`EnemyDefinition.visualPrefab` já aceita prefab).

## Comandos (com o editor Unity fechado)

```
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Projetos\Games\OiramRPG -executeMethod Oiram.EditorTools.VerticalSliceBuilder.BuildAll -logFile build.log
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Projetos\Games\OiramRPG -executeMethod Oiram.EditorTools.BalanceRunner.RunBatch -logFile balance.log
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -projectPath C:\Projetos\Games\OiramRPG -runTests -testPlatform EditMode -testResults editmode.xml
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -projectPath C:\Projetos\Games\OiramRPG -runTests -testPlatform PlayMode -testResults playmode.xml
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Projetos\Games\OiramRPG -executeMethod Oiram.EditorTools.BuildScript.BuildWindows -logFile winbuild.log
```
