# Recusa e tempo Collective — 25/09

## Regras confirmadas e implementadas

- Recusa força o ofertante a beber e segue a regra de Drink no Classic: seguro mantém iniciativa/cota; veneno passa ao próximo vivo; vitória encerra. Não há segunda janela de reação. Removidos `RefusalSuccession` e `testRefusalAsDrink`, que já não representam uma decisão aberta.
- Uma **rodada é uma Bandeja**. Pode haver várias **seleções coletivas** (reservar, Ready, beber) dentro dela. `RoundNumber` corresponde ao ID da Bandeja e `SelectionNumber` identifica a seleção atual, começando em 1 a cada troca.
- Prazo da Bandeja: `max(20, 60 - (rodada - 1) * 5)` segundos. Configurável em MatchConfig/GameManager. Resolver uma seleção sem trocar a Bandeja não reinicia o relógio.
- Ao acabar o tempo, o motor preserva reservas existentes, sorteia taças livres sem consultar conteúdo e entrega uma por jogador por passagem. Prioriza participantes sem nenhuma taça antes de distribuir extras. Quem já cumpriu sua obrigação é pulado; mortos não recebem.
- Após distribuir, os vivos ficam Ready e a seleção é resolvida em lote. Se a Bandeja ainda tiver o mínimo exigido, a próxima seleção é distribuída/resolvida automaticamente, sem ganhar outro tempo. Isso continua até trocar a Bandeja ou terminar a partida.
- DoubleDrink pode elevar a demanda além das taças existentes. O limite configurável de três bebidas por seleção permanece como parâmetro de playtest. Esgotar as taças permite Ready mesmo sem cumprir toda a exigência; nenhuma dívida é transferida para a Bandeja seguinte.
- Empate continua possível se todos os restantes morrerem no mesmo lote; não há vencedor intermediário nem desempate inventado.

## Mínimo de taças

`CollectiveMinimumCups = 0` usa o número de jogadores vivos. Um valor explícito maior antecipa a troca. O limite efetivo é `max(vivos, CollectiveMinimumCups)`, garantindo ao menos uma taça por sobrevivente na nova seleção. A troca ocorre se as sobras ficarem **abaixo** desse limite, não quando forem iguais.

Todos os pares de Bandeja configurados precisam ter ao menos o mínimo configurado e suportar os jogadores iniciais. Isso impede criar uma Bandeja que já nasce abaixo do mínimo.

Exemplo: mínimo 5, dois vivos, quatro taças restantes → troca. Mínimo 0, dois vivos, duas taças restantes → mantém e faz outra seleção.

## Exemplo solicitado

P1 e P4 já reservaram suas taças. Faltam três para P3 e duas para P2, com quatro livres.

O motor passa alternadamente por P2/P3 e distribui uma taça aleatória a cada um em cada passagem. Resultado: duas para P2 e duas para P3. P3 é dispensado da terceira porque não há taças. A ordem pode começar em P2 ou P3 conforme o ponto de partida na mesa; o motor avança o ponto inicial entre distribuições para evitar privilegiar sempre o mesmo assento quando sobra uma quantidade ímpar.

Reservas feitas manualmente permanecem válidas. A distribuição não toma uma taça já escolhida de alguém para redistribuí-la. Não foi introduzido um bloqueio novo para escolhas manuais por ordem de chegada.

## Mudanças de código

- `MatchConfig`: parâmetros de duração (60/5/20) e mínimo de taças, com validação de valores finitos e composições válidas. Removida a política pendente de Recusa.
- `ClassicTurnEngine`: Recusa é utilizável sem configuração experimental; mantém as validações de posse, fase e proibição de cadeia.
- `CollectiveTurnEngine`: separa rodada/seleção, guarda RemainingSeconds, completa escolhas por ciclos e aceita escassez. Ao cancelar uma reserva e liberar taça, retira Ready de quem só havia sido dispensado da exigência por escassez.
- `MatchEngine.AdvanceTime(seconds)`: operação do relógio autoritativo. Ao expirar, executa a sequência automática completa até a troca/encerramento e só depois entrega eventos. Não é uma ação controlada pelo jogador.
- `MatchEngine.CompleteCollectiveSelection()`: permite testar apenas a distribuição, sem consumo. O timeout usa a mesma implementação interna, seguida da resolução.
- `PlayerView`: expõe SelectionNumber e CollectiveRemainingSeconds, sem seed ou venenos secretos.
- `GameManager.Update`: chama AdvanceTime com tempo não escalado, somente para uma sessão Collective em andamento. A futura ponte NGO precisa fazer essa chamada apenas no host. Erros de ouvintes de timeout são registrados.
- `DebugControls`: adiciona menus para completar seleção e avançar tempo. `Resolve Selection` mantém a classe antiga `ResolveCollectiveRoundAction` por compatibilidade de API; agora o nome do menu deixa claro o que resolve.
- `GameEventKind.RoundDeadlineExpired`: fato da expiração por Bandeja. Os nomes legados RoundLocked/RoundResolved continuam representando os lotes de bebida; `Value` é SelectionNumber e `TrayId` identifica a rodada/Bandeja.

## Tempo, revisão e apresentação

O cronômetro não precisa de Unity: testes passam segundos explicitamente. No Editor, GameManager fornece esse tempo. O núcleo não usa animação ou frame específico para decidir dano.

Atualizações do contador, sem expiração, não alteram a revisão de gameplay. Isso evita tornar um pedido obsoleto a cada frame apenas porque o relógio andou. Quando há expiração/resolução, a revisão aumenta e todos os eventos são entregues com o estado final consistente.

Se um frame chega atrasado com um intervalo maior que o prazo restante, a Bandeja antiga é resolvida, mas a nova recebe seu prazo integral. Não se desconta tempo retroativo de uma Bandeja recém-apresentada, nem se pulam várias Bandejas por um frame atrasado. Com o jogo em Play e timeScale=0, o relógio não escalado continua; uma pausa de gameplay futura precisa de regra explícita.

## Testes

**52 cenários passaram, zero falhas**, com `dotnet run --project Tests/LethalDrink.Tests.csproj --no-restore`.

Novos cenários verificam: exemplo 3+2 com quatro taças; primeira taça antes de extras; preservação de reservas; Ready invalidado após cancelamento; expiração em 60 segundos; resolução automática de várias seleções; relógio preservado na mesma Bandeja; redução até o piso de 20; mínimo de reposição; empate no timeout; RNG reproduzível; ausência de reentrância; rejeição de tempos NaN/infinito/negativo e mínimos impossíveis.

O núcleo foi executado em .NET 10. Os componentes Unity ainda precisam de verificação no Editor. Esta revisão inclui os fontes e testes publicados junto deste documento.

## Limites preservados

DoubleDrink continua afetando o lote de bebidas atual, como antes; não foi estendido silenciosamente para reaplicar a penalidade em todas as seleções da mesma Bandeja. A mudança de nome rodada/Bandeja governa o relógio e o contador de rodadas. Se o item deve persistir até a troca, essa duração adicional precisa ser definida.

Continuam pendentes manipulação de jogadores Ready, combinações de itens reativos, Shuffle/Discard, desempate e a origem da rotação após Offer no Alternative. Essas pendências não impedem o cronômetro e a distribuição coletiva implementados aqui.

## Atualizar no Unity

Substitua os fontes de Core, Gameplay e Unity no seu projeto, preservando seus `.meta`. Não copie bin/obj ou csproj. A opção experimental de Recusa desapareceu; o item agora funciona diretamente. No GameManager, configure mínimo e tempos; entre em Play no modo Collective e acompanhe `State/Public`, ou use `Advance Time` para testar sem esperar um minuto.
