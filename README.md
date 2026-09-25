# unity-gproject-lethaldrink
Projeto inicial da ideia de jogo Lethal Drink, GDD será posteriormente vinculado a este documento

Ola Mundo

## Gameplay — revisão de 25/09

Os scripts atualizados estão em `Project Lethal Drink/Assets/LethalDrink`. Abra a pasta `Project Lethal Drink` no Unity Hub (versão registrada: 6000.3.17f1). Não é necessário copiar outra pasta de scripts para Assets.

- Recusa segue Drink do ofertante, sem segunda reação.
- Classic/Alternative sorteiam o primeiro jogador; o Console informa quem começa.
- A mesa informa apenas o total inicial de venenos, sem contador dinâmico.
- Collective: 60 segundos por Bandeja inicialmente, redução de 5 por troca até o mínimo de 20.
- No timeout, entrega taças aleatórias por ciclos, respeita escassez e resolve até trocar a Bandeja ou terminar a partida.
- O mínimo de reposição é configurável e nunca inferior ao número de vivos.

### Testes independentes de Unity

Requer SDK .NET 10. Execute na raiz do repositório:

```powershell
dotnet run --project Tests/LethalDrink.Tests.csproj
```

São 52 cenários de comportamento. O projeto de testes compila diretamente os fontes de Core/Gameplay dentro de Assets, sem duplicá-los. Ele não compila os componentes Unity; a validação da cena no Editor continua necessária.

### Documentação

- [Regras atuais, relógio e comandos de debug](docs/COLLECTIVE-TEMPO.md)
- [Esclarecimentos anteriores](docs/AJUSTES-25-09.md)
- [Relatório didático da fundação](docs/FASE-2.md)

Em GameManager, `Starting Player Override = 0` sorteia o início e `Use Fixed Seed` permite reproduzir testes. No Collective, ajuste `Collective Minimum Cups` e os campos de duração. Os menus de DebugControls incluem `Auto Complete Selection`, `Resolve Selection`, `Advance Time` e `State/Public`.

O Alternative ainda possui somente o fluxo circular de Drink; Offer/itens nesse modo aguardam a definição da origem da rotação após uma oferta. Esta publicação não apresenta essa extensão como concluída.
