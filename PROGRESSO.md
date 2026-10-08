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

### Visual toon (concluído)
- Shaders `Oiram/Toon` (faixas + contorno), `Water`, `Sky`, `Glow`; pós-processamento por cena (`SceneAtmosphere` + Volume).
- `MeshLibrary` (caixa arredondada, cone, icosfera facetada, gota, normais do contorno no UV3) e `MeshBuilder` (malhas com
  cor por vértice, convertidas para o espaço linear).
- Heróis chibi com rig (`CharacterRig`), chapéu/arma por job; inimigos com `Wiggle`; props, terreno em malha única,
  água com espuma, nuvens, partículas (`Fx`), tochas com `LightFlicker`, halos com `Billboard`.
- UI: fonte Fredoka (OFL), molduras 9-slice (`UiSkinGenerator`), ícones vetoriais (`ItemIcon`), logo animado.
- No editor, materiais e malhas gerados viram assets em `Materials/Generated` ao reconstruir as cenas (`EditorPalette`).

### Game feel (concluído)
- **Áudio gerado por código** (`Scripts/Audio`): sintetizador chiptune (`Synth`), ~30 efeitos + jingles de loot por raridade
  (`SoundBank`) e 7 músicas em loop compostas com seed fixa (`MusicComposer`); `AudioManager` com crossfade, *ducking*
  nos jingles e composição em thread de fundo. `OiramRPG ▸ Áudio ▸ Exportar WAVs` grava tudo em `Builds/Audio` com um
  resumo de volume (`volumes.csv`).
- **Anel de timing** sobre o alvo, encostando no círculo no instante do impacto (dourado = ataque, azul = defesa).
- **Impacto:** hit-stop + estrelas + tremor no PERFEITO, tremor em críticos/golpes de chefe/nocautes, números que estouram,
  poeira e som ao cair de pulos.
- **Menu de pausa** com 5 abas: nova **Itens** (consumíveis fora da batalha; não gasta se não fizer efeito) e nova **Opções**
  (volume de música/efeitos, anel, ms do timing — salvas em PlayerPrefs). Recados do menu aparecem dentro dele.
- **Salvamento automático** no mapa-múndi e no começo de cada andar; **Continuar** volta para o mesmo andar (mesmo mapa)
  ou para o mapa-múndi. Desligado em batchmode; o tour grava na pasta dele.

### Balanceamento
- `Docs/balanceamento.md`: **27/27 metas** — Vale (16) + dungeons (11). Ex.: Normal/Médio conclui 99%,
  Difícil/Médio 76%, Pesadelo/Experiente 67% (Pesadelo/Médio 4%); ~1 nível por descida Normal; uma descida Normal
  paga ~2,3 itens do ferreiro; a dificuldade se mantém do Nv 4 ao Nv 12.
- `BalanceGuardTests` falha se alguma meta quebrar.

### Testes e verificação
- **85 testes**: 78 EditMode (regras, loot, jobs, gerador de dungeons, escala, save/load, lojas, guarda de balanceamento,
  sintetizador/músicas, itens fora da batalha, salvamento automático) + 7 PlayMode (batalha automática, mapa ↔ batalha,
  menu de pausa, título/mapa-múndi, cidade + baú escondido, **dungeon completa**, **Continuar volta ao andar salvo**).
- Tour de screenshots (`OiramRPG.exe -oiram-tour <pasta>`) passa por título, Vale, menus (Itens/Opções), mapa-múndi,
  lojas, baú escondido, dungeon e o anel de timing.
- Repositório: https://github.com/DanielPitthan/OiramRPG (branch `main`).

## O que ainda não foi validado por uma pessoa

- **Playtest humano pendente** — roteiro em `Docs/playtest.md` (agora inclui cidade e dungeons).
- O simulador decide bem nos menus e limpa todas as salas; jogadores reais erram mais (jogo um pouco mais difícil).
- O simulador não troca de job nem usa Ladino/Aprendiz; essa progressão só tem testes unitários.
- O áudio foi conferido por medição (pico/RMS de cada som), não de ouvido: vale ouvir e opinar no playtest.

## Pendências conhecidas (pequenas)

- O build é *development*: atalhos F1–F6 e log de playtest ativos (`BuildOptions.Development` em `Editor/BuildScript.cs`).
- Avisos de shaders de pós-processamento no log do player (renderer do template URP; inofensivos).

## Próximos marcos sugeridos

1. Playtest humano e calibração dos timed hits com os logs (agora com som, anel e opções).
2. Mais cidades (cada uma com seu baú escondido), mais dungeons/temas.
3. Chefes com mecânicas (fases, fraquezas), conjuntos ou lendários fixos se quiser.
4. Mais variedade visual por dungeon (props temáticos) e animações de habilidade específicas.

## Comandos (com o editor Unity fechado)

```
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Projetos\Games\OiramRPG -executeMethod Oiram.EditorTools.VerticalSliceBuilder.BuildAll -logFile build.log
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Projetos\Games\OiramRPG -executeMethod Oiram.EditorTools.BalanceRunner.RunBatch -logFile balance.log
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -projectPath C:\Projetos\Games\OiramRPG -runTests -testPlatform EditMode -testResults editmode.xml
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -projectPath C:\Projetos\Games\OiramRPG -runTests -testPlatform PlayMode -testResults playmode.xml
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Projetos\Games\OiramRPG -executeMethod Oiram.EditorTools.BuildScript.BuildWindows -logFile winbuild.log
```
