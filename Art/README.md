# Арт We Are Legion

Редаговані джерела в Aseprite. Unity бере PNG з `Assets/Resources/Art` (імпорт налаштовує `Assets/Scripts/Editor/ArtImportSettings.cs`: Point, без стиснення, 100 px на одиницю).

| Файл | Що всередині |
|---|---|
| `ant_parts.aseprite` | мураха для конструктора: шари ніг (basic / cargo / fast), тіла (simple / armored / light), щелепи (basic / digger / fighter); кадр 1 — стоїть, 2–5 — біг (теги `stand`, `run`). Видима базова збірка, інші варіанти сховані. |
| `beetle.aseprite` | жук-охоронець, 2 кадри ходи |
| `queen.aseprite`, `egg.aseprite` | королева 48×48, яйце 16×16 |
| `items.aseprite` | склад, 3 крихти, м'ясо — кожне окремим шаром |
| `icon_hunger`, `hp_back`, `cargo_crumb`, `cargo_meat` | значок голоду, рамка смужки HP, вантаж над головою |

Назва шару = назва PNG, тому шари не перейменовуй (код шукає спрайти за цими іменами).

## Після правок

З кореня проєкту:

```
"C:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe" -b --script-param dir=Art --script-param out=Assets/Resources/Art --script Art/export_art.lua
```

Скрипт перезапише PNG усіх шарів, Unity підхопить їх сам.
