# Esclarecimentos e ajustes de 25/09

> Esta é a primeira revisão do dia. A confirmação posterior de Recusa e o relógio do Collective estão em [COLLECTIVE-TEMPO.md](COLLECTIVE-TEMPO.md) e prevalecem sobre as pendências de Recusa/DoubleDrink e o resultado de 41 testes registrados abaixo.

## Implementado e verificado

- A mesa informa **InitialPoisonCount**, que não muda durante uma Bandeja. Consumir veneno ou purificar uma taça não altera esse valor. Foram removidos o contador público dinâmico e sua representação na visão/debug.
- Purifier continua garantindo segurança sem revelar o conteúdo anterior. O comentário antigo sobre contagem “conservadora” não se aplica mais à regra atual.
- Collective: após calcular e aplicar todos os danos e verificar vitória/empate, se a partida continuar e houver menos taças que jogadores vivos, a Bandeja é substituída automaticamente. Se houver quantidade suficiente, conserva as sobras. Nenhuma reposição é realizada antes de todos resolverem a rodada atual ou após encerramento.
- Classic e Alternative sorteiam o primeiro jogador uma vez por partida. A escolha é do motor autoritativo, não da UI. Collective não tem um único jogador inicial.
- `MatchConfig.StartingPlayerId = null` significa sortear; um ID explícito é override de laboratório. `Seed = null` cria uma sequência nova; uma seed explícita permite reprodução determinística.
- GameManager expõe `Use Fixed Seed` (padrão desmarcado) e `Starting Player Override` (padrão 0, sortear). O Console informa o primeiro jogador. Uma repetição do mesmo jogador em duas partidas é possível e normal; aleatório não significa alternar obrigatoriamente.
- Os testes antigos fixam seed e P1 explicitamente. Novos testes verificam a contagem inicial invariável, ausência de contador dinâmico na visão, sorteio reproduzível que alcança os quatro assentos, override, ausência de inicial no Collective e conservação/reposição correta das sobras.

Resultado: **41 cenários passaram, zero falhas**, executando `dotnet run --project Tests/LethalDrink.Tests.csproj --no-restore`. Unity não executado nesta revisão. Mudanças estão no workspace local; nenhuma alteração foi enviada ao GitHub.

## Confirmações solicitadas antes dos próximos ajustes

1. **Recusa no Classic:** P1 oferece, P3 recusa, P1 bebe forçado. A pergunta pendente é quem joga depois: seguir Drink (seguro mantém P1, veneno passa ao próximo vivo) ou passar sempre? O código mantém a opção experimental explícita até confirmar. A devolução da bebida e a ausência de uma segunda reação já estão implementadas.
2. **DoubleDrink e disputa:** a nova regra permite encerrar a obrigação quando acabarem as taças, sem rejeitar o efeito por demanda global. Falta confirmar se reservamos a primeira taça de cada jogador antes de permitir extras, ou se a ordem de escolha pode deixar alguém sem nenhuma. A lógica antiga de capacidade ainda não foi substituída enquanto essa resposta estiver pendente. Depois disso, Ready deve aceitar a obrigação reduzida pela escassez e voltar a validar se uma reserva for cancelada e liberar taça.
3. **Alternative:** o usuário aprovou as mesmas ações/itens do Classic, mudando a passagem de turnos. Falta confirmar se, após P1 oferecer a P3, a rotação avança a partir de P1 (próximo P2) ou de P3 (próximo P4). A extensão das ofertas e reações será feita com essa regra explícita, evitando implementar uma variante não desejada. O controlador mínimo anterior permanece até essa confirmação.

Empate coletivo por eliminação simultânea continua implementado. Nenhum desempate foi criado: o usuário ainda avaliará seu design.

## O que significavam os termos

**Entrada serial em uma única thread:** o motor termina um pedido antes de começar o próximo. Isso protege validação/reserva contra duas alterações simultâneas. O host futuro pode receber pedidos simultâneos de vários jogadores, mas os enfileira e processa em ordem. `busy` bloqueia chamadas feitas durante eventos; não é um mecanismo para chamadas concorrentes de threads diferentes. A rodada Collective continua sendo calculada como um lote: todos os danos antes da avaliação de vitória.

**Custos que encerram iniciativa:** um item pode consumir sua unidade de inventário e também encerrar o turno, sem permitir Drink/Offer em seguida. São custos distintos. Não há item concreto aprovado com esse efeito atualmente; o enum `EndsInitiative` é uma preparação, não uma regra ativa. Para ativá-lo é necessário escolher o item e seu resultado no fluxo. Isso não bloqueia os itens já implementados.

## Arquivos alterados

`Core/MatchConfig.cs`, `Core/Tray.cs`, `Core/Rules.cs`, `Gameplay/MatchEngine.cs`, `Gameplay/ClassicTurnEngine.cs`, `Gameplay/AlternativeTurnEngine.cs`, `Gameplay/CollectiveTurnEngine.cs`, `Gameplay/PlayerView.cs`, `Unity/GameManager.cs`, `Unity/DebugControls.cs`, `Tests/Program.cs`, README e documentação.

Não é necessário copiar o projeto .csproj nem bin/obj para Unity. Substitua os fontes correspondentes em Assets/LethalDrink, preservando seus .meta já criados no projeto Unity. Verifique o novo sorteio pelo log de início antes de enviar ações de P1.
