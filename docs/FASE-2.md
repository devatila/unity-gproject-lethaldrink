# Lethal Drink — relatório da implementação da Fase 2

> Registro da entrega inicial de 24/09. As decisões posteriores estão em [AJUSTES-25-09.md](AJUSTES-25-09.md) e [COLLECTIVE-TEMPO.md](COLLECTIVE-TEMPO.md) e prevalecem sobre este registro, incluindo contagem pública, sorteio, reposição, Recusa, relógio e resultados dos testes.

## 1. Resumo geral

A pasta de trabalho estava vazia. Portanto, todos os arquivos físicos são novos: nenhum script existente no disco foi apagado. A comparação “antes/depois” refere-se aos 12 scripts do anexo da Fase 1.

Foi preservada a fundação: C# puro, IDs, estados separados da apresentação, ações como intenções e eventos. A principal divisão é entre MatchEngine (regras comuns) e controladores Classic/Collective/Alternative (fluxo). Foram implementados seleção cancelável, oferta pendente, reação, consumo controlado, dano, eliminação, vitória/empate, Bandejas exatas e cinco itens. Casos de design não fechado retornam erro explícito ou exigem configuração experimental.

## 2. Nova arquitetura

```text
Unity: GameManager / DebugControls     Futuro: UI / IA / ponte NGO
                  \                         /
                   IGameAction (intenção)
                            |
                  MatchEngine.ExecuteAction
                            |
                  validação global + revisão
                            |
           +----------------+----------------+
           |                |                |
    ClassicTurnEngine CollectiveTurnEngine AlternativeTurnEngine
           |                |                |
           +----------------+----------------+
                            |
            operações internas de MatchEngine
             vidas / itens / conteúdo / RNG
                            |
             estado consistente + Revision++
                            |
                 eventos públicos imutáveis
                            |
                    apresentação Unity

Saída de dados: MatchView pública / PlayerView por destinatário
```

Os controladores usam composição: guardam uma referência ao motor, não herdam dele. Não há interface de modo com implementação vazia: três ramificações explícitas no ponto de entrada são suficientes. Core não referencia Gameplay; as projeções que conhecem MatchEngine ficam em Gameplay.

Não há classe MatchState separada: `Status`, `Outcome`, `WinnerId`, estados de jogadores e Bandeja já têm um único proprietário. Um recipiente adicional duplicaria acesso sem resolver um problema atual.

## 3. Árvore de arquivos

```text
.editorconfig
.gitignore
NuGet.Config
README.md
docs/
  FASE-2.md
LethalDrink/
  LethalDrink.csproj
  LethalDrink.asmdef
  Core/
    Rules.cs
    ActionResult.cs
    PlayerState.cs
    CupState.cs
    ItemDefinition.cs
    MatchConfig.cs
    Tray.cs
    GameEvent.cs
  Gameplay/
    Actions.cs
    MatchEngine.cs
    ClassicTurnEngine.cs
    CollectiveTurnEngine.cs
    AlternativeTurnEngine.cs
    PlayerView.cs
  Unity/
    LethalDrink.Unity.asmdef
    GameManager.cs
    DebugControls.cs
Tests/
  LethalDrink.Tests.csproj
  Program.cs
```

`bin` e `obj` são resultados locais de build ignorados pelo Git. Não fazem parte da importação Unity.

## 4. Remoções, renomes e mudanças de API

| Antes | Problema | Depois | Benefício |
|---|---|---|---|
| TurnEngine guardava tudo e decidia turnos Classic | Outros modos duplicariam regras ou deformariam a classe | MatchEngine + três controladores | Dano, vitória e taças são únicos |
| IGameAction expunha CanExecute/Execute | Chamadas diretas podiam desviar de eventos e controle central | IGameAction carrega ator; MatchEngine.CanExecute/ExecuteAction controlam execução | Uma entrada autoritativa e sem validação duplicada |
| GameActionBase só repetia a interface | Nenhum comportamento compartilhado | Omitida | Menos uma abstração |
| Drink/Offer recebiam CupId e consumiam imediatamente | Não existia seleção ou reação | SelectCupAction guarda seleção; Drink/Offer confirmam o estado selecionado | Seleção não é consumo |
| UseItemAction recebia apenas ator/item | Alvos diferentes não cabiam no contrato | Ações específicas em Actions.cs | Parâmetros obrigatórios visíveis |
| ItemState.OwnerPlayerId | Segunda fonte de propriedade | IDs em PlayerState.Inventory | Posse tem uma única autoridade |
| PlayerState.CannotTargetPlayerId | Restrição sobrevivia à janela errada | Classic.CannotOfferToPlayerId | Estado temporário no fluxo certo |
| IsPoisoned público e imutável | Purificação precisava mudar conteúdo; estado podia vazar | Conteúdo interno mutável pelo motor + CupView sem segredo | Purifier e privacidade coexistem |
| SetCurrentPlayer/StartNewTurn públicos | Transições parcialmente diferentes e chamadas arbitrárias | StartInitiative privado e continuação explícita | Cota e restrição têm semânticas distintas |
| Eventos disparados durante mutação | Apresentação podia interromper/reentrar | Fatos acumulados e publicados após commit | Observadores veem estado consistente |
| GameManager sorteava taças | Configuração/apresentação tomavam decisão autoritativa | Motor gera Bandeja a partir de MatchConfig | RNG fica na lógica comum |

As classes de ações foram reunidas em um arquivo porque são pequenas mensagens de dados, sem algoritmos. É possível separá-las posteriormente por navegação; isso não altera arquitetura. A lógica que antes estava em cada ação continua explícita nos controladores, que agora precisam conhecer seleção e reação.

## 5. Arquivos novos

Todos os arquivos da árvore são novos no disco. Conceitualmente, são novos os controladores Collective/Alternative, MatchConfig, Tray, definições de itens, snapshots, eventos estruturados, projetos de compilação, testes, asmdefs e documentação.

## 6. Arquivos evoluídos em relação ao anexo

PlayerState, CupState, ActionResult, ações, GameManager e DebugControls preservam suas intenções, com APIs atualizadas. ItemState agora está junto de ItemDefinition. TurnEngine foi dividido. GameActionBase e UseItemAction genérico não foram reproduzidos. Não há shim da API antiga porque não havia código consumidor em disco para preservar; ao integrar em um projeto existente, migre os chamadores conforme a tabela anterior.

## 7. Explicação de cada arquivo

### Core/Rules.cs

**Responsabilidade:** vocabulário fechado de modos, fases, erros e políticas. **Por que existe:** evita strings mágicas e espalhamento das mesmas decisões. **Alterações:** acrescenta Running/Ended, Winner/Draw, flags de timing e modos, políticas experimentais. **Fluxo:** configuração e controladores leem os enums. **C#:** enum é um conjunto de nomes; Flags utiliza bits independentes. **Exemplo:** Purifier aceita `Proactive | Reactive`; Refusal aceita somente Reactive.

`Classic = 1`, `Collective = 2`, `Alternative = 4`: em binário, 001, 010 e 100. A operação `|` combina permissões; `&` testa se uma permissão está presente. `None = 0` não permite nada. Não usar 1, 2, 3 para três flags independentes: 3 já combina os dois primeiros bits. TargetType é separado de Timing: “qual alvo” e “quando usar” são perguntas diferentes.

`ItemCost.EndsInitiative` é somente vocabulário preparado. Nenhum item aprovado nesta entrega tem esse custo e não há regra de sucessão implementada para ele. Não basta mudar uma definição para ativar custo: o fluxo correspondente precisa ser decidido e implementado.

### Core/ActionResult.cs

**Responsabilidade:** sucesso/falha e motivo estruturado. **Por que existe:** UI/testes comparam ErrorCode sem interpretar português. **Alterações:** Error acompanha Success e Message. **Fluxo:** validação → chamador. **C#:** readonly struct pequeno por valor, propriedades sem setters públicos, métodos estáticos Ok/Fail. **Exemplo:** P1 tenta selecionar durante a reação de P3 e recebe ReactionNotAllowed; nenhuma taça é alterada.

### Core/PlayerState.cs

**Responsabilidade:** identidade, vidas e inventário. **Por que existe:** mantém dados universais fora dos modos. **Alterações:** remove restrição de alvo e métodos públicos de dano/cura; adiciona IDs de itens. **Fluxo:** MatchEngine cria/atualiza; consumidores leem. **C#:** List privada com ReadOnlyCollection pública; internal permite mutação somente no assembly do núcleo. **Exemplo:** usar Refusal remove seu ID do inventário de P3 e marca a instância usada.

Um wrapper de leitura impede `Inventory.Add`, mas não é uma cópia histórica. PlayerView, ao contrário, produz cópias para apresentação. O asmdef separa o assembly Unity; seus componentes não podem chamar SetLives interno.

### Core/CupState.cs

**Responsabilidade:** identidade persistente, posição, consumo e conteúdo autoritativo. **Por que existe:** taça não é a sua posição nem a seleção de alguém. **Alterações:** setters internos; IsPoisoned interno; IsPurified público; RevealedPoison só preenchido no consumo. **Fluxo:** motor cria, purifica, move e resolve; modos guardam somente os IDs selecionados/reservados. **C#:** encapsulamento por assembly. **Exemplo:** Cup5 muda da posição 4 para 1 no Swap, mas continua Cup5 e conserva seu conteúdo.

Purifier altera o conteúdo atual para seguro e marca a garantia pública. Não guarda um campo público “era veneno”, pois isso revelaria a informação que o item deve ocultar.

### Core/ItemDefinition.cs

**Responsabilidade:** descreve tipos e instâncias. **Por que existe:** metadados comuns não devem ser repetidos em cada cópia. **Alterações:** ItemState passa a ter ID, Definition e IsUsed; sem proprietário. **Fluxo:** catálogo → criação de instância → validação de modo/timing/posse → consumo. **C#:** Dictionary, objetos de configuração imutáveis, referência compartilhada à definição. **Exemplo:** itens 17 e 22 podem compartilhar Definition de Refusal sem compartilhar consumo.

O catálogo contém somente Inspection, Purifier, Refusal, Swap e DoubleDrink. Heal/ForceDrink antigos não são implementados automaticamente. Nenhum ScriptableObject é necessário.

### Core/MatchConfig.cs

**Responsabilidade:** regras/configurações da sessão e pares válidos de Bandeja. **Por que existe:** retira números do GameManager e permite testes de hipóteses. **Alterações:** novo objeto imutável; valida 2–4 jogadores, capacidades, configurações de taça e enums. **Fluxo:** GameManager/testes criam; motor copia listas e usa os valores. **C#:** parâmetros opcionais, validação de construtor, readonly struct para TrayConfiguration, coleções de leitura. **Exemplo:** oferecer `{8,3}` e `{10,4}` permite sortear um desses pares, não combinar tamanho de um com veneno do outro.

Os defaults de cota 1, capacidade 3, máximo DoubleDrink 3 e pular ofertante são de playtest. Refusal começa Undecided; SuddenDeath começa Disabled. O pool é filtrado pela disponibilidade do modo. A seed faz parte da configuração autoritativa, nunca do snapshot público.

### Core/Tray.cs

**Responsabilidade:** Bandeja atual, composição inicial, taças restantes e contagem pública de venenos. **Por que existe:** torna o ciclo explícito. **Alterações:** substitui o dicionário global de taças sem ciclo. **Fluxo:** MatchEngine.CreateTray cria; resoluções usam; RenewEmptyTray substitui. **C#:** coleção de leitura, LINQ Count, nullable int. **Exemplo:** após purificar sem revelar conteúdo anterior, PublicRemainingPoisons fica null: não afirmar um número que vazaria segredo.

Essa contagem é conservadora. Não implementa um solucionador de inferência; pode permanecer desconhecida mesmo quando uma dedução posterior permitiria estreitar o valor. Isso não altera a verdade autoritativa nem o número inicial divulgado.

### Core/GameEvent.cs

**Responsabilidade:** fatos públicos imutáveis com revisão e Bandeja de origem. **Por que existe:** Unity recebe o que aconteceu mesmo se o motor já iniciou outra Bandeja. **Alterações:** substitui vários delegates específicos por um evento tipado com enum; não há Event Bus. **Fluxo:** Emit acumula; Publish entrega após mutações. **C#:** objeto imutável, nullable IDs. **Exemplo:** CupDrunk informa jogador, taça e conteúdo já revelado; não contém inspeção secreta.

`Value` é um campo compacto cujo significado depende de Kind; a tabela de eventos abaixo é seu contrato. Se os fatos crescerem muito, classes de evento específicas podem substituir esse campo. Não são necessárias agora.

### Gameplay/Actions.cs

**Responsabilidade:** mensagens de intenção. **Por que existe:** humano, IA e debug expressam pedidos do mesmo modo. **Alterações:** IGameAction não altera estado; IItemAction acrescenta ItemId. GameActionBase foi removida. **Fluxo:** chamador cria → ExecuteAction valida e encaminha. **C#:** interfaces, construtores, sealed, propriedades somente leitura, pattern matching no controlador. **Exemplo:** `new OfferCupAction(1, 3)` confirma a taça atualmente selecionada por P1; não escolhe secretamente outra taça.

As ações específicas são SelectCup, CancelCupSelection, DrinkCup, OfferCup, UseInspectionItem, UsePurifier, UseRefusal, UseSwapItem, UseDoubleDrink, ReserveCup, CancelReservation, SetReady e ResolveCollectiveRound. Reunir dados pequenos evita uma classe base sem implementação e evita parâmetros opcionais sem relação entre si.

### Gameplay/MatchEngine.cs

**Responsabilidade:** autoridade comum e entrada serial de ações. **Por que existe:** todos os modos compartilham jogadores, taças, inventário, RNG, dano, vitória e eventos. **Alterações:** extrai o universal de TurnEngine; centraliza validação global e publicação. **Fluxo:** CanExecute → controlador.Validate → controlador.Apply → operações internas → Revision++ → eventos. **C#:** Dictionary, composição, delegates/eventos, try/finally, pattern matching, nullable. **Exemplo:** o Collective envia vários pares jogador/taça a ResolveDrinks; o motor calcula todos os danos antes de testar quem sobreviveu.

`busy` bloqueia chamadas feitas por ouvintes enquanto o lote de eventos é publicado. Cada ouvinte é isolado por try/catch; erros ficam em NotificationErrors e o GameManager os registra. Uma exceção de UI não desfaz ou interrompe uma regra já resolvida. Não há rollback genérico de exceções internas inesperadas: esse é um motor pequeno de operações validadas, não um banco de dados transacional.

### Gameplay/ClassicTurnEngine.cs

**Responsabilidade:** iniciativa, seleção, oferta pendente, reação, restrição e cota. **Por que existe:** são regras Classic, não universais. **Alterações:** introduz fases e PendingDrink; remove o significado ambíguo de “SetCurrentPlayer”. **Fluxo:** valida a intenção na fase atual; chama motor para efeitos comuns; decide sucessão. **C#:** enum de fase, nullable seleção, composição. **Exemplo:** P1 oferece para P3; CurrentPlayerId segue 1, Pending.TargetId é 3 e só CanPlayerReact(3) retorna true.

PendingDrink é imutável: CupId/OriginatorId/TargetId não podem ser trocados depois do commit. Não é necessário um ReactionContext adicional com os mesmos dados. A fase Reaction mais PendingDrink representa esse contexto.

### Gameplay/CollectiveTurnEngine.cs

**Responsabilidade:** reservas, quantidade exigida e Ready por rodada. **Por que existe:** não há um único jogador atual. **Alterações:** novo controlador, sem copiar dano/vitória. **Fluxo:** reserva exclusiva → itens → reserva restante → Ready → ResolveCollectiveRound → lote de consumo/danos → nova rodada. **C#:** Dictionary CupId→PlayerId, HashSet de Ready, somas de demanda, cópia de escolhas com ToArray. **Exemplo:** dois DoubleDrink aumentam RequiredDrinkCount(1) para 3; a demanda global também aumenta em 2 e precisa caber nas taças restantes.

Resolver é um pedido explícito, aceito de qualquer jogador vivo somente quando todos estão Ready. O último Ready não inicia automaticamente a resolução neste laboratório. Nenhuma espera de animação ocorre dentro do motor.

### Gameplay/AlternativeTurnEngine.cs

**Responsabilidade:** prova mínima de rotação circular. **Por que existe:** demonstra que Classic não precisa ser alterado para outro fluxo. **Alterações:** novo; não implementa Offer/itens especulativos. **Fluxo:** selecionar/cancelar/beber → consumo comum → próximo vivo mesmo se seguro. **C#:** composição e nullable seleção. **Exemplo:** P1 bebe seguro e P2 recebe o turno. Outros pedidos retornam DesignPending com MODE DESIGN INCOMPLETE.

### Gameplay/PlayerView.cs

**Responsabilidade:** snapshots públicos e conhecimento privado autorizado. **Por que existe:** apresentação e clientes não devem receber o estado verdadeiro inteiro. **Alterações:** novo; ItemView/PlayerPublicView/CupView/MatchView/PlayerView contêm somente dados de saída. **Fluxo:** host solicita snapshot público ou de um destinatário; transporte futuro envia somente esse objeto. **C#:** cópias defensivas, ReadOnlyDictionary, IReadOnlyDictionary, projeções LINQ. **Exemplo:** Inspection de P1 adiciona Cup2 somente ao KnownCupContents de P1; a visão de P2 não recebe o booleano.

GetPlayerView(id) não autentica ninguém. É uma API do processo autoritativo, não um endpoint que clientes possam chamar livremente. IA deve receber a visão e ações possíveis, não uma referência irrestrita ao motor/DebugSecretState. Ações inspecionáveis ainda podem ser validadas por CanExecute no host.

### Unity/GameManager.cs

**Responsabilidade:** montar e substituir a sessão Unity. **Por que existe:** ponto de entrada da cena. **Alterações:** não cria taças nem sorteia veneno; só constrói config e motor; remove assinaturas ao trocar sessão/destruir. **Fluxo:** Awake define singleton → Start cria debug → ExecuteAction encaminha → HandleEvent imprime. **C#:** MonoBehaviour, SerializeField, singleton limitado, inscrição/remoção de delegate. **Exemplo:** escolher Collective no Inspector cria MatchEngine com CollectiveTurnEngine; nenhum botão aplica dano diretamente.

### Unity/DebugControls.cs

**Responsabilidade:** laboratório manual de ações e cheats. **Por que existe:** testar regras antes da UI. **Alterações:** expõe seleção, reação, itens específicos, reservas, Ready e visões pública/secreta distintas. **Fluxo:** menu de contexto → objeto de ação → GameManager; cheats chamam API Debug do motor. **C#:** atributos Unity, expressão de propriedade, LINQ para impressão. **Exemplo:** GiveItem concede Refusal a P3; PrintPublicState mostra seu ID; UseRefusal envia ator/ID sem editar taça diretamente.

### Projetos, asmdefs e arquivos de apoio

- **LethalDrink.csproj:** compila somente Core/Gameplay, linguagem C# 9, alvo de verificação .NET 10. Existe para compilar sem Unity; não substitui o projeto gerado pelo Editor.
- **LethalDrink.asmdef:** cria assembly puro com noEngineReferences. Impede introduzir UnityEngine no domínio sem um erro de compilação no Editor.
- **Unity/LethalDrink.Unity.asmdef:** assembly de apresentação referencia o núcleo. Isso dá significado real aos setters internal: Unity está fora dessa fronteira.
- **Tests/LethalDrink.Tests.csproj:** executável que referencia o núcleo; nenhuma biblioteca de teste externa.
- **Tests/Program.cs:** cenários de comportamento; Check/Ok/Error lançam falha e o runner registra nome/resultado e retorna exit code. Não testa getters isoladamente. Cada teste cria sua própria partida para não depender da ordem.
- **NuGet.Config:** fontes vazias, pois não há pacotes. Permite reproduzir sem downloads de bibliotecas; não elimina consultas de configuração da própria ferramenta.
- **.editorconfig:** indentação e expansão de blocos C# para facilitar leitura.
- **.gitignore:** exclui artefatos binários e intermediários.
- **README.md:** entrada rápida, comandos e limites da importação Unity.
- **docs/FASE-2.md:** este relatório; especifica contrato, migração, auditoria e pendências. Não executa regras.

## 8. Fluxos completos do Classic

### Classic A — Drink seguro

1. `ExecuteAction(new SelectCupAction(1, 2))` valida ator/taça; Classic.SelectedCupId vira 2, fase CupSelected. Cup2 não é consumida.
2. `ExecuteAction(new DrinkCupAction(1))` valida a fase e chama Classic.ResolveDrink.
3. MatchEngine.ResolveDrinks consome Cup2 e calcula zero dano.
4. Classic limpa seleção/restrição e mantém P1 e a cota atual.
5. Eventos saem com o estado consistente. Nenhum novo TurnStarted é emitido porque a iniciativa não mudou.

### Classic B — Offer seguro e liberação da restrição

1. P1 seleciona Cup3 e oferece para P2.
2. PendingDrink guarda Cup3/P1/P2; fase Reaction; P1 continua CurrentPlayerId, mas seus pedidos são bloqueados.
3. P2 envia DrinkCupAction. O controlador escolhe Cup3 pendente, encerra a reação e manda resolver.
4. Sendo segura, StartInitiative(2, 1) dá iniciativa a P2, zera a cota de sua nova posse e proíbe alvo P1.
5. P2 seleciona outra taça; Offer para P1 falha sem consumo.
6. P2 confirma Drink seguro; permanece P2, mas CannotOfferToPlayerId torna-se null. A cota proativa NÃO reinicia.
7. P2 pode selecionar uma terceira taça e oferecer a P1.

### Classic C — Recusa

Pré-condição explícita do experimento: `RefusalSuccession.ResolveAsOriginatorDrink`. Sem ela, a ação retorna DesignPending e preserva item/oferta.

1. P1 seleciona Cup5 e oferece para P3.
2. P3 envia UseRefusalAction com um item que realmente está em seu inventário.
3. O motor valida tipo, posse, pool, timing e contexto antes de consumir o item.
4. Classic.ResolveDrink(true) usa OriginatorId como bebedor, elimina PendingDrink e resolve Cup5 diretamente.
5. Não chama EnterReaction novamente: não existe uma segunda janela para P1 recusar.
6. Na hipótese configurada, seguro mantém P1 e sua cota; veneno passa ao próximo vivo a partir de P1; vitória encerra primeiro. Essa sucessão precisa de aprovação de design antes de virar padrão.

### Classic D — Purifier reativo

1. P1 oferece uma taça para P3.
2. P3 usa Purifier naquela CupId pendente; outro alvo é rejeitado.
3. O item é consumido; IsPoisoned vira false e IsPurified vira true. Nenhum retorno informa o conteúdo anterior.
4. A reação permanece aberta para P3 beber. A tentativa de combinar outro item reativo retorna DesignPending.
5. P3 envia Drink; o resultado seguro transfere a iniciativa para P3 e aplica a restrição de oferta segura.

## 9. Sistema de reação

Reaction não significa turno do alvo. CurrentPlayerId continua originador e CanPlayerReact consulta Pending.TargetId. Toda ação passa pela fase antes de executar. P1 não consegue selecionar, cancelar, usar item, mudar alvo nem beber em nome próprio enquanto P3 decide.

Purifier não encerra reação; Drink e Refusal encerram. Refusal consome e resolve numa única operação, sem cadeia. Não há timeout, disconnect ou scheduler automático: essas decisões continuam pendentes para networking.

## 10. Itens

| Item | Modos do catálogo | Timing | Alvo validado | Efeito |
|---|---|---|---|---|
| Inspection | Classic/Collective | Proativo | Taça disponível | Atualiza somente conhecimento privado do ator |
| Purifier | Classic/Collective | Proativo/reativo | Selecionada/pendente no Classic; própria reserva no Collective | Garante segurança sem revelar conteúdo anterior |
| Refusal | Classic | Reativo | Oferta pendente ao ator | Força originador a beber; sem segunda reação |
| Swap | Classic/Collective | Proativo | Duas taças distintas disponíveis; reservas Collective aguardam decisão | Troca posições, preserva IDs/conteúdo |
| DoubleDrink | Collective | Proativo | Jogador vivo ainda não Ready | Incrementa exigência se demanda total couber |

A validação verifica item existente, tipo esperado pela ação, não usado, pertencente ao inventário do ator, pool/modo, timing e cota quando aplicável. Somente depois remove inventário/marca usado. Dados de alvo inválidos não consomem item.

No Classic a cota acompanha a sequência inteira de iniciativa, não cada taça bebida. Reações não gastam essa cota. No Collective ainda não se definiu uma cota gratuita equivalente: o laboratório não aplica automaticamente a cota Classic. Não há distribuição automática, frequência por Bandeja nem ItemDistributor sem regra aprovada; DebugGiveItem valida capacidade e duplicata de tipo.

## 11. MatchEngine

Responsabilidades comuns não se repetem: ResolveDrinks, EvaluateMatch, CreateTray, ValidateItem, ConsumeItem, Inspect, Purify e Swap estão aqui. Ele não conhece GameObject, Animator, NetworkBehaviour, UI ou Steam.

GetNextAlivePlayerId retorna int?; null significa ausência/origem inválida. Em resolução normal, antes de acessar Value, o fluxo já avaliou que a partida segue com mais de um vivo. O parâmetro avoid tem fallback: só exclui o ofertante enquanto existir alternativa elegível.

ExecuteAction aceita expectedRevision opcional. Se a revisão observada pelo chamador ficou velha, rejeita sem mutação. Isso ajuda pedidos atrasados, mas não é um protocolo de deduplicação completo: sem revisão, duas intenções válidas são duas ações. A ponte futura precisa autenticar remetente, correlacionar IDs e serializar entrada.

## 12. ClassicTurnEngine

O controlador guarda exatamente o temporário: Phase, CurrentPlayerId, SelectedCupId, Pending, CannotOfferToPlayerId, ProactiveItemsUsed e ReactiveItemUsed. PlayerState não contém esses estados.

StartInitiative inicia outra posse, limpa/substitui restrição e zera cota. Drink seguro mantém a posse e limpa apenas a restrição. Offer venenoso sobrevivido transfere ao alvo sem restrição. Offer fatal usa NextAfterOfferElimination, método visível e pequeno com opção SkipOfferOriginatorOnKill.

## 13. CollectiveTurnEngine e fluxo completo

1. P1/P2/P3/P4 reservam respectivamente taças 1/2/3/4; cada exigência começa em 1.
2. P2 usa DoubleDrink em P1: demanda de P1 passa a 2, global a 5.
3. P3 usa outro DoubleDrink em P1: demanda de P1 passa a 3, global a 6.
4. P1 reserva mais duas taças. Antes disso não consegue ficar Ready.
5. Todos enviam SetReadyAction. Reservas de jogadores Ready não podem mudar sem Unready.
6. ResolveCollectiveRoundAction exige AllReady, captura todas as reservas e gera RoundLocked.
7. ResolveDrinks calcula conteúdo de todas, agrega dano por jogador, aplica todos os danos, registra eliminações e só então avalia a partida.
8. Se os dois últimos morrerem, Outcome=Draw e WinnerId=null. Nunca é emitida vitória intermediária.
9. RoundResolved é registrado; se continuar, renova Bandeja totalmente vazia e inicia rodada com exigência 1. Se restarem taças, mas menos que vivos, entra AwaitingTrayDecision.

O lock é lógico e síncrono durante ExecuteAction, sem expor uma fase animável que possa alterar o resultado. RoundLocked é um fato publicado depois do commit; a apresentação pode tocar a sequência a partir dos eventos, mas não continuar a decidir danos.

## 14. Alternative

MODE DESIGN INCOMPLETE. Apenas seleção, cancelamento e Drink estão disponíveis. Após seguro ou veneno, se a partida continuar, a iniciativa vai ao próximo vivo circularmente. Usa as mesmas Bandejas, dano, vitória e eventos. Offer e itens retornam DesignPending, sem inventar diferenciais.

## 15. Bandejas

CreateTray escolhe uniformemente um dos pares configurados, cria exatamente X venenos em N posições e embaralha o vetor. IDs não são reutilizados entre Bandejas; posições reiniciam de zero. O RNG pertence ao motor.

Última taça Classic: resultado e dano → encerramento se houver vencedor → sucessão normal se continuar → TrayEnded → nova Bandeja → TrayStarted. Não cria Bandeja extra após vitória. A animação do bartender apenas representa essa mudança já decidida. A cota de iniciativa não reinicia só pela troca.

No Collective o mesmo comportamento vale quando zerou a Bandeja; a substituição com sobras é explicitamente pendente. O cheat ForceTrayEnd abandona reservas/seleções/reação e é apenas laboratório, não a política de produção.

## 16. GameManager

CreateDebugMatch usa os números do Inspector para construir MatchConfig. StartSession cria o novo motor antes de descartar assinaturas da sessão anterior, evitando destruir a sessão válida por configuração inválida. OnDestroy remove inscrições e singleton. A sessão não imprime segredos automaticamente.

Não há cena nem asset Unity serializado nesta entrega. O componente precisa ser colocado numa cena do projeto real seguindo README. O singleton permanece só como conveniência de laboratório.

## 17. DebugControls

Menus Classic servem também à seleção/Drink de Alternative; menus Collective expõem reservas, cancelar, Ready/Unready e resolver. Os itens têm parâmetros específicos no objeto enviado. PrintPublicState mostra IDs, vidas, inventários, posições e estado sem venenos secretos. PrintPlayerKnowledge e PrintSecretState são rotulados HOST DEBUG ONLY.

Cheats nunca são chamados pelas regras de gameplay. Ainda são métodos públicos do objeto autoritativo; a futura bridge deve usar uma lista explícita de pedidos de gameplay e não expor esses métodos.

## 18. Eventos e momento do disparo

Todos os eventos são entregues depois de mutações e Revision++. Uma consulta ao motor em um handler vê o estado final da ação inteira, inclusive a próxima Bandeja/rodada se já começou. Para representar fatos da Bandeja anterior, use os IDs/revisão do evento.

| Kind | Dados relevantes | Quando é registrado |
|---|---|---|
| CupSelected | PlayerId, CupId | Seleção validada |
| SelectionCancelled | PlayerId | Seleção desfeita |
| CupOffered | Originador em PlayerId, alvo em TargetId, CupId | PendingDrink criado; também representa início de reação |
| ReactionEnded | Alvo anterior, CupId | Drink ou Recusa encerra contexto |
| CupDrunk | Bebedor, CupId, Value=1 veneno/0 seguro | Conteúdo consumido e agora público |
| LivesChanged | PlayerId, Value=vidas | Dano agregado ou cheat |
| PlayerEliminated | PlayerId | Vidas chegam a zero |
| TurnStarted | PlayerId | Iniciativa muda |
| TrayEnded | TrayId antigo | Esgotamento ou substituição de debug |
| TrayStarted | TrayId novo, Value=quantidade | Nova Bandeja preparada |
| ItemUsed | PlayerId, Value=ItemId | Item validado consumido; sem resultado privado |
| CupPurified | CupId | Garantia pública de segurança |
| CupsSwapped | CupId=primeira, Value=segunda | Posições trocadas |
| ReservationChanged | PlayerId, CupId, Value=1/0 | Reservar/cancelar |
| ReadyChanged | PlayerId, Value=1/0 | Ready/Unready |
| RoundLocked/Resolved | Value=rodada | Resolução coletiva |
| MatchEnded | PlayerId=vencedor ou null, Value=Outcome | Avaliação após todos os danos |
| DebugChanged | PlayerId, Value=ItemId | Concessão de item pelo laboratório |

EventOccurred leva GameEvent; ActionResolved leva a intenção e ActionResult para ações aceitas. Rejeições são retornadas diretamente e não disparam evento: não houve mudança pública. A criação inicial ocorre antes de haver assinantes; GameManager/UI devem obter GetPublicView depois de StartSession em vez de esperar um evento inicial.

## 19. Testes criados

Tests/Program.cs contém cenários isolados de seleção/cancelamento, Drink seguro/veneno, mortos, oferta pendente, bloqueio de originador/terceiros, restrição e liberação, retaliação após veneno, sucessão provisória, Recusa, Purifier proativo/reativo, combinação reativa pendente, cota de iniciativa, Bandejas, vitória, conhecimento privado, Swap, posse/capacidade/duplicatas, ação inválida sem mutação, eventos/reentrância, revisão obsoleta, busca nullable, reservas, DoubleDrink acumulado/capacidade/teto, Ready, batch/empate, sobras pendentes, Alternative, SuddenDeath opcional e validação de configuração.

Não há dependência de cena, rede ou relógio real. A composição exata é verificada em 25 Bandejas de dois motores com a mesma seed. Zero venenos/todas venenos são fixtures de teste e não sugestões de balanceamento.

## 20. Resultados de execução

Execução inicial: **37 cenários passaram, zero falhas**, em .NET SDK 10.0.301. Núcleo compilou com zero avisos/erros antes da formatação. Consulte também a saída final entregue junto da revisão desta documentação.

Limite: Unity/GameManager e DebugControls não fazem parte do .csproj puro e não foram executados no Editor. A validação extra de .NET Standard 2.1 encontrou ausência de NETStandard.Library.Ref nesta máquina; mantivemos o runner .NET 10 sem acrescentar downloads/dependências. Isso não demonstra nem refuta compatibilidade com uma versão específica do Unity.

## 21. Problemas encontrados durante implementação

- O diretório não continha os scripts nem projeto Unity. Foi necessária criação dos fontes, sem migração de cenas/metadados.
- A sucessão após ForcedDrink da Recusa continua não especificada. Foi criada opção explícita Undecided/ResolveAsOriginatorDrink para não inventar uma resposta padrão.
- Purifier e contagem pública exata entram em conflito se o conteúdo anterior deve permanecer desconhecido. A contagem passa a nullable em vez de revelar quantos venenos o item removeu.
- A demanda do DoubleDrink precisa considerar jogadores que ainda não reservaram. Validar somente “existe uma taça livre” permitiria deixar outro jogador sem taça obrigatória; a validação usa soma global.
- Publicar eventos durante dano coletivo permitiria vitória falsa/observação parcial. Resultados e dano são resolvidos antes da entrega de fatos.
- Swap de reserva, alterações depois de Ready e itens reativos múltiplos precisam de regras adicionais. Esses casos retornam DesignPending, não sucesso fictício.
- O sandbox bloqueou leituras da configuração NuGet da conta; builds/restores executáveis exigiram permissão. Não houve instalação de bibliotecas.

## 22. Nova auditoria

| Gravidade | Situação após implementação | Consequência/ação |
|---|---|---|
| CRÍTICO antes de rede | Autenticação e proteção contra cliente não existem nesta fase | Não publicar MatchEngine/Config/Debug APIs; bridge deve autenticar e enviar visões filtradas |
| IMPORTANTE | Unity ainda não validado | Importar fontes/asmdefs, verificar compilação e ciclo da cena |
| IMPORTANTE | Refusal padrão Undecided | Laboratório exige escolha explícita; fechar sucessão para distribuição ao jogador |
| IMPORTANTE | Collective pausa com sobras insuficientes | Não é uma partida completa de produção enquanto política não for escolhida |
| IMPORTANTE | Entrada pressupõe uma thread | busy protege reentrância, não concorrência; futura bridge deve enfileirar/serializar |
| IMPORTANTE | Exceções internas inesperadas não possuem rollback | Ações são pré-validadas; futuros efeitos precisam respeitar validar antes de mutar |
| IMPORTANTE | Knowledge expõe memória perfeita por CupId na visão do dono | A representação de memória humana e Shuffle continua pendente; não implementar rastreamento automático de Shuffle |
| MELHORIA | Contagem pública fica conservadoramente desconhecida após Purifier | Poderá ganhar intervalos/dedução simples após regra de informação aprovada |
| MELHORIA | GameEvent.Value muda de significado por Kind | Contrato documentado; migrar a eventos específicos somente se complexidade crescer |
| MELHORIA | ItemCost.EndsInitiative não tem item/execução associada | Não configurar custo sem aprovar e implementar seu fluxo |
| MELHORIA | Dicionário retém instâncias consumidas | Memória proporcional a itens concedidos na sessão; aceitável no escopo, revisar em sessões muito longas |
| MELHORIA | Config só cria IDs 1..N e nomes Player N | Lobby futuro poderá aceitar descritores de jogador sem mudar regras |
| COSMÉTICO | Mensagens/nomes de debug misturam idiomas | Padronizar/localizar na fase de apresentação |

Não foram encontrados bugs críticos de resolução nos cenários automatizados executados. Isso não prova ausência de bugs nem cobre comportamento de rede/Editor. Nenhuma IA, timeout, reconexão ou distribuição de itens foi apresentada como pronta.

## 23. DECISÕES DE GAME DESIGN AINDA PENDENTES

Cada linha registra o comportamento atual e o que precisa ser decidido antes da expansão correspondente.

| Decisão | Por que importa | Código atualmente | Opções | Decidir antes de avançar |
|---|---|---|---|---|
| Sudden Death | Letalidade e duração do duelo | Disabled por padrão; FatalPoison/DoubleDamage opt-in | Fatal, dano 2, sem mudança | Ativação desde partida de 2 e efeito final |
| Sucessão após Offer fatal | Controle social da iniciativa | Pula originador quando há alternativa, configurável | Ordem pura ou exclusão com fallback | Resultado de playtest |
| Sucessão após Refusal | Quem controla depois de forçar originador | Undecided; política experimental explícita | Tratar como Drink ou outra transferência | Seguro, veneno, eliminação e restrição |
| Composição das Bandejas | Probabilidades e duração | Lista explícita de pares válidos | Tabelas por modo/vivos/iniciais | Balanceamento final por 2/3/4 jogadores |
| Teto DoubleDrink | Evita dogpile | 3, configurável | 2/3/outro limite | Teto final e se autoalvo continua permitido |
| Empate Collective | Todos podem morrer juntos | Draw sem vencedor | Empate final, revanche temática | Tratamento de produto/apresentação |
| Troca com sobras Collective | Pode faltar uma taça por vivo | AwaitingTrayDecision, sem descarte silencioso | Repor tudo, completar, resolver sobras | Política e destino das taças restantes |
| Conhecimento após Shuffle | Informação versus memória | Shuffle ausente; Inspection por identidade | Apagar, preservar, limitar interface | Regra antes de implementar Shuffle |
| Discard revela conteúdo? | Dedução pública | Discard ausente | Revelar ou manter secreto | Contrato de informação |
| Pool final | Escopo e equilíbrio | Cinco tipos de laboratório | Selecionar 4–6 finais | Aprovar quais entram e em quais modos |
| Distribuição de itens | Ritmo/capacidade | Somente GiveItem de debug; 3 espaços, sem duplicatas | Inicial, por Bandeja, outros intervalos | Frequência, quantidades e pool |
| Alternative completo | Identidade do modo | Só Drink circular | Offer, itens e composição específicos | Regras antes de acrescentar efeitos |
| Itens reativos múltiplos | Purifier seguido de Refusal muda risco | Segundo item bloqueado com DesignPending | Um item, vários ou combinações | Combinações permitidas e custo |
| Restrição no duelo | Pode restringir único alvo | Ativa por padrão, flag para playtest | Ativa/desativada | Regra de 2 jogadores |
| Cota proativa | Duração da posse/rodada | Classic 1 por posse; Collective sem limite equivalente | Uma por rodada, por fase ou livre | Quota própria do Collective |
| Ready e itens contra alvo | Ready pode significar compromisso ou disponibilidade | DoubleDrink em Ready retorna DesignPending | Invalidar Ready, bloquear ou reabrir | Janela oficial de manipulação |
| Swap com reservas | Reserva segue identidade ou lugar? | Bloqueado nas taças reservadas | Seguir ID, posição ou proibir | Regra antes de habilitar |
| Purifier no Collective | Momento e alvo | Apenas taça própria reservada no laboratório | Qualquer taça, própria, outro timing | Aprovar escopo de alvo |
| Timeout/disconnect | Reação pode esperar indefinidamente | Sem automação | Auto Drink, pausa ou eliminação | Regras antes de networking jogável |
| Item que encerra iniciativa | Precisa escolher sucessor | Só enum preparado | Próximo físico/outra política | Item concreto e fluxo antes de ativar custo |

## 24. Próximos passos

1. Importar no Unity e executar os fluxos A–D e Collective pelos menus, com a política experimental de Recusa visível.
2. Fechar sucessão da Recusa, sobras Collective e manipulações envolvendo Ready/reservas.
3. Escolher pool/distribuição provisória aprovada; então adicionar um distribuidor pequeno se necessário.
4. Playtest de quota, composição e dupla/tripla bebida usando MatchConfig.
5. Somente depois integrar ponte NGO: mapear conexão→jogador, validar revisão/ID de pedido e filtrar snapshots. O núcleo permanece C# puro.
6. IA deve receber PlayerView e produzir as mesmas intenções; não receber DebugSecretState ou seed.
