-- Збирає .aseprite файли (шари, кадри, теги) з PNG у Assets/Resources/Art.
-- Запуск: Aseprite.exe -b --script-param src=<Assets/Resources/Art> --script-param out=<Art> --script build_art.lua
local src = app.params["src"]
local out = app.params["out"]

local function img(name) return Image{ fromFile = src .. "/" .. name .. ".png" } end

-- layers: знизу вгору; frames: імена PNG по кадрах; hidden: сховати шар (інші варіанти частини)
local function build(file, w, h, frameCount, layers, tags)
  local spr = Sprite(w, h, ColorMode.RGB)
  for i = 2, frameCount do spr:newEmptyFrame() end
  for i = 1, frameCount do spr.frames[i].duration = 0.1 end
  for i, L in ipairs(layers) do
    local layer = (i == 1) and spr.layers[1] or spr:newLayer()
    layer.name = L.name
    for f, png in ipairs(L.frames) do spr:newCel(layer, f, img(png), Point(0, 0)) end
    if L.hidden then layer.isVisible = false end
  end
  for _, t in ipairs(tags or {}) do
    local tag = spr:newTag(t[2], t[3]); tag.name = t[1]
  end
  spr:saveAs(out .. "/" .. file .. ".aseprite")
  spr:close()
  print("saved " .. file .. ".aseprite")
end

local function frames(base, n)
  local t = {}
  for i = 0, n - 1 do t[#t + 1] = base .. "_" .. i end
  return t
end

-- Мураха для конструктора: 5 кадрів (1 — стоїть, 2..5 — біг), шари частин; видима базова збірка.
build("ant_parts", 32, 32, 5, {
  { name = "ant_legs_basic",  frames = frames("ant_legs_basic", 5) },
  { name = "ant_legs_cargo",  frames = frames("ant_legs_cargo", 5), hidden = true },
  { name = "ant_legs_fast",   frames = frames("ant_legs_fast", 5),  hidden = true },
  { name = "ant_body_simple", frames = { "ant_body_simple" } },
  { name = "ant_body_armored", frames = { "ant_body_armored" }, hidden = true },
  { name = "ant_body_light",  frames = { "ant_body_light" },  hidden = true },
  { name = "ant_jaw_basic",   frames = { "ant_jaw_basic" } },
  { name = "ant_jaw_digger",  frames = { "ant_jaw_digger" },  hidden = true },
  { name = "ant_jaw_fighter", frames = { "ant_jaw_fighter" }, hidden = true },
}, { { "stand", 1, 1 }, { "run", 2, 5 } })

build("beetle", 32, 32, 2, { { name = "beetle", frames = { "beetle_0", "beetle_1" } } }, { { "walk", 1, 2 } })
build("queen", 48, 48, 1, { { name = "queen", frames = { "queen" } } })
build("egg", 16, 16, 1, { { name = "egg", frames = { "egg" } } })
build("items", 32, 32, 1, {
  { name = "storage", frames = { "storage" } },
  { name = "crumb_0", frames = { "crumb_0" } },
  { name = "crumb_1", frames = { "crumb_1" }, hidden = true },
  { name = "crumb_2", frames = { "crumb_2" }, hidden = true },
  { name = "meat",    frames = { "meat" },    hidden = true },
})
build("icon_hunger", 12, 12, 1, { { name = "icon_hunger", frames = { "icon_hunger" } } })
build("hp_back", 18, 4, 1, { { name = "hp_back", frames = { "hp_back" } } })
build("cargo_crumb", 6, 5, 1, { { name = "cargo_crumb", frames = { "cargo_crumb" } } })
build("cargo_meat", 7, 6, 1, { { name = "cargo_meat", frames = { "cargo_meat" } } })
