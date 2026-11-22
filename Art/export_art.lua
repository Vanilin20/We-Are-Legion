-- Експорт арту для Unity: кожен шар кожного .aseprite у папці Art -> PNG у Assets/Resources/Art.
-- Шар з одним кадром -> <шар>.png; з кількома -> <шар>_0.png, <шар>_1.png ...
-- Запуск (з кореня проєкту): Aseprite.exe -b --script-param dir=Art --script-param out=Assets/Resources/Art --script Art/export_art.lua
local dir = app.params["dir"] or "Art"
local out = app.params["out"] or "Assets/Resources/Art"

for _, file in ipairs(app.fs.listFiles(dir)) do
  if file:match("%.aseprite$") then
    local spr = app.open(app.fs.joinPath(dir, file))
    for _, layer in ipairs(spr.layers) do
      if not layer.isGroup then
        local cels = {}
        for _, cel in ipairs(layer.cels) do cels[#cels + 1] = cel end
        table.sort(cels, function(a, b) return a.frameNumber < b.frameNumber end)
        for _, cel in ipairs(cels) do
          local name = layer.name
          if #cels > 1 then name = name .. "_" .. (cel.frameNumber - 1) end
          local im = Image(spr.spec)  -- повне полотно, щоб шари лишались вирівняними
          im:drawImage(cel.image, cel.position)
          im:saveAs(app.fs.joinPath(out, name .. ".png"))
        end
      end
    end
    spr:close()
    print("exported " .. file)
  end
end
