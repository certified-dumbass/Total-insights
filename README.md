# Total Insights voor Jellyfin

De beheerpagina telt de beschikbare, geïndexeerde films, afleveringen, overige video's en muziekvideo's op. Een Jellyfin-item telt serverbreed één keer op basis van de item-ID. Afzonderlijk geïndexeerde videobestanden met verschillende ID's kunnen beide meetellen. Items zonder bekende speelduur tellen mee in het aantal en dragen nul uren bij. Bibliotheken worden afzonderlijk opgeteld. De serielijst toont de 25 grootste series op basis van de gezamenlijke speelduur van hun afleveringen. Maanden en jaren gebruiken het gemiddelde gregoriaanse jaar van 365,2425 dagen.

## Functies

- Beheerdersdashboard met de totale speelduur in uren, dagen, weken, maanden en jaren.
- Ranglijst van bibliotheken en de 25 series met de meeste speelduur.
- Aanpasbaar Discord-rapport met een voorbeeld en handmatig verzenden.
- Sjabloonvelden: `{hours}`, `{days}`, `{weeks}`, `{months}`, `{years}`, `{items}`, `{topLibrary}` en `{topSeries}`.

## Bouwen

Voor je Jellyfin 12.4-server heb je de .NET 10 SDK nodig. Voer in deze projectmap uit:

```powershell
dotnet build -c Release
```

De DLL staat daarna in `bin/Release/net10.0/LibraryRuntime.dll`. Je kunt de broncode ook naar een eigen GitHub-repository pushen. De meegeleverde Actions-workflow probeert vier afzonderlijke builds en zet elke geslaagde DLL bij de artifacts. De workflow publiceert geen releases.

| Doel | SDK | Buildargumenten |
| --- | --- | --- |
| Jellyfin 10.10.7 | .NET 8 | `-p:JellyfinVersion=10.10.7 -p:PluginTargetFramework=net8.0` |
| Jellyfin 10.11.x | .NET 9 | `-p:JellyfinVersion=10.11.0 -p:PluginTargetFramework=net9.0` |
| Jellyfin 12.0 | .NET 10 | `-p:JellyfinVersion=12.0.0 -p:PluginTargetFramework=net10.0` |
| Jellyfin 12.4 | .NET 10 | Standaardinstelling |

Gebruik de DLL die is gebouwd voor jouw serverreeks. Deze broncode is voorbereid op de targets hierboven. Controleer per target of de build slaagt en Jellyfin de plugin laadt.

## Installeren en gebruiken

1. Stop Jellyfin.
2. Maak `C:\ProgramData\Jellyfin\Server\plugins\LibraryRuntime_1.0.0.0` en plaats de gebouwde DLL rechtstreeks in die map.
3. Start Jellyfin. Open **Dashboard → Plugins → Total Insights** als beheerder.
4. Controleer de totale speelduur, de bibliotheken en de series.
5. Vul een Discord-webhook in, bewerk het bericht en klik op **Voorbeeld**. Sla de instellingen op voordat je het rapport naar Discord stuurt.

De webhook blijft op de server opgeslagen. Versturen gebeurt handmatig vanuit het dashboard. Houd de webhook privé.
