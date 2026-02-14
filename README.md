# ST Diagram Studio

Modern, gelistirilebilir bir WPF diagram editor prototipi.

## Proje Yapisi

```text
.
|-- App.xaml
|-- App.xaml.cs
|-- diagram.csproj
|-- Models/
|   |-- DiagramNode.cs
|   |-- DiagramEdge.cs
|   |-- DiagramModel.cs
|   `-- DiagramProjectFile.cs
`-- Views/
    |-- MainWindow.xaml
    |-- MainWindow.xaml.cs
    |-- MainWindow.Theme.cs
    |-- MainWindow.Persistence.cs
    |-- MainWindow.Interaction.cs
    `-- MainWindow.Rendering.cs
```

## Katmanlar

- `Models/`: Domain modeli, node/edge kurallari, import/export verisi.
- `Views/MainWindow.xaml`: UI yerlesimi.
- `Views/MainWindow.Theme.cs`: Tema ve renk yonetimi.
- `Views/MainWindow.Persistence.cs`: Save/load, history, export ve proje durumu.
- `Views/MainWindow.Interaction.cs`: Secim, baglama, surukleme ve etkilesim akislar.
- `Views/MainWindow.Rendering.cs`: Canvas render ve node gorsel uretimi.

## Calistirma

```powershell
dotnet build
dotnet run
```

## Guvenlik ve Git Ignore

`.gitignore` dosyasi asagidaki riskleri engelleyecek sekilde guncellendi:

- Derleme ciktilari ve IDE metadata dosyalari (`bin/`, `obj/`, `.vs/`, `.vscode/`, `.idea/`)
- Ortam dosyalari (`.env`, `.env.*`)  
  Not: `.env.example` ve `.env.sample` commit edilebilir.
- Yerel config/sir dosyalari (`appsettings.Development.json`, `appsettings.*.local.json`, `secrets*.json`)
- Sertifika ve anahtar dosyalari (`*.key`, `*.pem`, `*.pfx`, `*.p12`, `*.jks`, vb.)
- Bulut credential klasorleri (`.aws/`, `.azure/`, `.gcp/`)
- Kisisel dokumanlar (`*.docx`, `*.xlsx`, `*.pdf`, vb.)

Oneri:

- Gercek secret degerlerini repoya koyma.
- Gerekli ayarlar icin yalnizca ornek sablon dosyalari (`*.example`, `*.sample`) paylas.
