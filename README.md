# ST Diagram Studio


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

## Node Aciklamalari

- Her node icin `Description` alani desteklenir.
- Sol panelde `Create Node` ve `Selected Node` bolumlerinden aciklama ekleyebilir/guncelleyebilirsiniz.
- Node uzerine gelince detayli aciklama tooltip olarak gosterilir.
- `Add Node` ile eklenen yeni node'lar aktif gorunen calisma alaninda olusturulur.
