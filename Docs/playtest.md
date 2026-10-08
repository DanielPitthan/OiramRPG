# Guia de playtest

O balanceamento já foi ajustado com o simulador (veja [balanceamento.md](balanceamento.md)), mas duas coisas
só uma pessoa jogando consegue responder: **o tempo dos timed hits parece justo?** e **o jogo é divertido no ritmo atual?**
Este playtest leva uns 40–60 minutos.

## Como jogar

- Rode `Builds/Windows/OiramRPG.exe`, ou abra a cena `Title` no Unity e aperte Play.
- O jogo **grava sozinho** um log da sessão (só no editor e em development builds) em
  `%USERPROFILE%\AppData\LocalLow\DefaultCompany\OiramRPG\playtest\`. Não precisa fazer nada.

## Roteiro

1. Comece pelo **título ▸ Novo jogo** e jogue normalmente até derrotar o Golem, sem usar os atalhos F1–F5.
   Tente os timed hits em todos os ataques e também **defender** (apertar Confirmar quando o inimigo acerta você).
2. Saia pela placa perto do Golem para o **mapa-múndi** e vá à **Vila Ventura**: visite as quatro lojas, converse
   com os moradores e tente achar o **baú escondido**. Salve na pousada.
3. Faça a **Mina Abandonada no Normal**. Depois tente uma dungeon no **Difícil** (e, se quiser sofrer, no Pesadelo).
   Volte à vila entre as descidas para ver o estoque novo do ferreiro.
4. Feche o jogo **no meio de uma dungeon** e use **Continuar** no título: você deve voltar ao começo do mesmo andar
   (o jogo salva sozinho no mapa-múndi e a cada andar; a pousada também salva).
5. Experimente pelo menos uma vez: a *Investida* do Oiram (segurar e soltar), o menu **Jobs** (aprender uma habilidade com JP)
   e equipar algo do **Inventário**.
6. Lá pela metade, abra **Tab ▸ Opções** e ligue **Mostrar ms do timing** (ou aperte **F6**): os popups passam a
   mostrar quantos milissegundos você apertou cedo (−) ou tarde (+). Jogue um pouco também com o **anel de timing**
   desligado e veja se ainda acerta.
7. Use uma Poção pelo menu (**Tab ▸ Itens**) depois de uma batalha difícil.
8. Se perder, continue: no Vale você acorda na fogueira; numa dungeon, no mapa-múndi — sempre com o loot coletado.

## O que observar (responda em poucas palavras)

- Batalhas comuns: fáceis demais / ok / difíceis? Longas demais?
- Golem: quantas tentativas? Pareceu justo?
- Timed hits: o "PERFEITO" acontece quando você sente que acertou? Alguma ação parece impossível de acertar?
  O anel ajuda ou atrapalha? O golpe perfeito "pesa" (congelada, estrelas, som)?
- Som: alguma música cansa rápido? Algum efeito irritante ou alto demais?
- Loot: empolgante? Muito item inútil? Você trocou de equipamento com frequência?
- Dungeons: os mapas aleatórios ficaram variados? Cada dificuldade pareceu diferente? Valeu a pena arriscar o Difícil?
- Cidade: os preços fazem sentido? O apostador é divertido? Achou o baú escondido sozinho?
- Jobs/JP: entendeu o sistema? Sentiu vontade de trocar de job?
- Algo confuso, travado ou feio?

## Depois de jogar

Me diga "terminei o playtest" com suas respostas. Eu leio o log direto do seu computador e rodo a análise.
Se quiser ver você mesmo: no Unity, **OiramRPG ▸ Balanceamento ▸ Analisar logs de playtest** gera
`Docs/playtest-analise.md` com:

- sua precisão real em cada tipo de timed hit (Perfeito/Bom/erro, desvio mediano e dispersão em ms);
- uma sugestão de calibração da latência (`timingLatencyCompensation` em `Data/BalanceConfig`), se você aperta sempre cedo ou tarde;
- as batalhas que você jogou (rodadas, dano, nocautes, duração);
- uma previsão do simulador para a rota inteira **com o seu perfil de habilidade**.
