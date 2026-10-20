# Interface 1.1 Backport — для SPT 4.1.x

*(English below)*

## Что это

Интерфейс Escape from Tushonka 1.0–1.2 в SPT 4.1.x (клиент 0.16.9). Экраны пересобраны по данным самих 1.0 и 1.2:
расположение, размеры, цвета, иконки и тексты взяты из их UI-сцен. Игровые элементы 0.16 остаются и работают как
раньше. Каждую часть можно выключить отдельно: F12 → Grifm0le-Interface11Backport.

**Общий вид**
- Главное меню: колонка кнопок 1.0, логотип без «BETA TESTING», выбор фона, без баннера беты.
- Кнопки 1.0 с «потёртой» плашкой при наведении, окна и ячейки предметов 1.2, перерисованные иконки 1.2.
- Экран загрузки при запуске игры: четыре новые картинки 1.2 и строка копирайта 1.2.

**До и после рейда**
- Выбор стороны и локации, ключ доступа, настройки оффлайн-рейда, страховка и экран готовности — как в 1.2.
- Загрузка рейда как в 1.0: этап, полоса из сегментов, время загрузки.
- В рейде: список выходов, сообщения о выходе и событиях, экран PostFX — как в 1.2.
- Экраны после рейда как в 1.2: итог, убийства, статистика, опыт, лечение.

**Персонаж**
- Вкладка ПЕРСОНАЖ (1.2): выбор лица своей стороны с большим превью и голос с прослушиванием.
- Одежда в ячейках с картинками, вкладка заданий, список умений без неработающих умений, окна инвентаря — как в 1.2.

**Торговцы и барахолка**
- Задания торговца по группам: уровни лояльности, ключевые, оперативные. В задании каждая награда в своей ячейке, ниже —
  «Награда в следующих заданиях».
- Одежда у Барахольщика: шапка и фильтр 1.2 («Все», «Куплено», «Доступно», «Закрыто»). Издание Unheard получает
  переключатели BEAR/USEC и одежду обеих сторон. Другим изданиям её включает команда Командо (ниже).
- Окно фильтров барахолки, переключатель «Торговцы / Барахолка», иконки валют — как в 1.2.

**Прочее**
- Настройки в одну колонку, как в 1.2, с подсказками 1.2 при наведении: что делает настройка и как она влияет на
  CPU, GPU и RAM.
- Сообщения: вкладки «Частота ЧВК» и «Спецсвязь», даты над сообщениями.
- Строка «Не для продажи на барахолке» в описании предмета.
- VoIP в оффлайн-рейдах без предупреждения о недоступности.

Все надписи мода — на 17 языках игры.

## Команда для Командо

В «Сообщениях» напишите другу **Commando**:
- `spt clothing on` — Барахольщик показывает этому персонажу одежду обеих сторон (BEAR и USEC), как в издании Unheard.
- `spt clothing off` — снова только одежда своей стороны.

## Установка

Распакуйте `Interface11Backport-<версия>.zip` в папку SPT (где лежат `BepInEx` и `SPT_Runtime`):
- клиентская часть — `BepInEx/plugins/Interface10`;
- серверная часть — `SPT_Runtime/user/mods/Interface10`.

Серверная часть нужна для смены лица, одежды обеих сторон и «Награды в следующих заданиях». Остальное работает и без неё.

## Чего нет

Только из живой игры: сезоны, магазин, режимы игры, несколько персонажей, наблюдение за союзниками, отображение
престижа, основной сюжет, расширения, диалоги с торговцами (они есть в VisitAPI), Area Light Instancing.

## Совместимость

Сделан и проверен на чистом SPT 4.1.6. Моды, которые меняют те же экраны, могут конфликтовать — пишите, исправим.
С VisitAPI работает: вкладку заданий персонажа строит сам VisitAPI, его сюжетные задания у торговцев скрыты.
С Fika 2.4.6 проверен в роли хоста рейда; совместная игра с другими игроками пока не проверялась.
С Hj's Improved Customization UI: используются его вкладка CUSTOMIZATION и переключатели BEAR/USEC у Барахольщика, наши
вкладка «Персонаж» и изменения у Барахольщика (и их серверная часть) отключаются сами.
С WTT - Head and Voice Selector: его выбор головы и голоса на вкладке «Общее» работает под нашими ячейками одежды.
Со Skills Extended: первая помощь, полевая медицина, бесшумные и теневые операции остаются в списке умений — этот мод
даёт им смысл. Если другой мод оживляет другие скрытые умения, выключите «1.2 skills list» (F12).

## Авторы и права

Мод: Grifm0le. Код и переводы на 15 языков написаны с помощью ИИ-ассистента (Claude, Anthropic).
Иконки, картинки, тексты и расположение экранов оригинальной игры принадлежат Battlestate Games.
Код мода — под лицензией MIT (LICENSE.txt).

---

## What it is

The interface of Escape from Tushonka 1.0–1.2 in SPT 4.1.x (client 0.16.9). The screens are rebuilt from 1.0's and 1.2's
own data: layouts, sizes, colours, icons and texts come from their UI scenes. 0.16's elements stay and work as before.
Each part can be switched off on its own: F12 → Grifm0le-Interface11Backport.

**Look**
- Main menu: 1.0's button column, the logo without "BETA TESTING", the background of your choice, no beta banner.
- 1.0's buttons with the worn plate on hover, 1.2's windows and item cells, 1.2's redrawn icons.
- The loading screen at the game's start: 1.2's four new pictures and 1.2's copyright line.

**Before and after a raid**
- Side and location selection, key access, offline raid settings, insurance and the ready screen as in 1.2.
- Raid loading as in 1.0: the stage, a progress bar in segments, the loading time.
- In the raid: the exits list, the extraction and event messages, the PostFX screen as in 1.2.
- The screens after a raid as in 1.2: result, kills, statistics, experience, treatment.

**Character**
- The CHARACTER tab (1.2): your side's faces with a large preview, and the voice with a button to hear it.
- Clothing in cells with pictures, the tasks tab, the skills list without the skills that do nothing, the inventory
  windows as in 1.2.

**Traders and flea market**
- Trader tasks in groups: loyalty levels, key tasks, operational tasks. In a task each reward has its own cell, with
  "Reward in the next tasks" under them.
- Ragman's clothing: 1.2's header and filter (All, Obtained, Available, Locked). The Unheard Edition gets the BEAR/USEC
  toggles and the clothing of both sides; for other editions the Commando command below turns it on.
- The flea market's filter window, the Traders / Flea market toggle and the currency icons as in 1.2.

**Other**
- Settings in one column as in 1.2, with 1.2's tooltips on hover: what a setting does and how it weighs on the CPU, GPU
  and RAM.
- Messenger: the PMC Frequency and Special Communications tabs, dates over the messages.
- The "Not for sale on the flea market" line in item descriptions.
- VoIP in offline raids without the "unavailable" warning.

Every text of the mod is in the game's 17 languages.

## Commando command

In Messenger, write to the **Commando** friend:
- `spt clothing on` — Ragman shows this character the clothing of both sides (BEAR and USEC), as for the Unheard Edition.
- `spt clothing off` — your side's clothing only again.

## Install

Extract `Interface11Backport-<version>.zip` into your SPT folder (the one with `BepInEx` and `SPT_Runtime`):
- the client part goes to `BepInEx/plugins/Interface10`;
- the server part to `SPT_Runtime/user/mods/Interface10`.

The server part is needed for the face change, the clothing of both sides and "Reward in the next tasks". Everything
else works without it.

## Not included

Live-only: seasons, the store, game modes, several characters, spectating teammates, the prestige display, the main
story, expansions, trader dialogues (VisitAPI has them), Area Light Instancing.

## Compatibility

Made and tested on a clean SPT 4.1.6. Mods that change the same screens may clash — report it and it gets fixed.
Works with VisitAPI: VisitAPI builds the character's tasks tab itself, and its story tasks stay hidden at the traders.
Fika 2.4.6: tested as the raid's host; playing together with other players is not tested yet.
With Hj's Improved Customization UI, its CUSTOMIZATION tab and Ragman's BEAR/USEC toggles are used; this mod's
CHARACTER tab and Ragman changes (and their server side) switch themselves off.
With WTT - Head and Voice Selector, its head and voice drop-downs on the Overall tab work under this mod's clothing cells.
With Skills Extended, first aid, field medicine, silent operations and shadow connections stay in the skills list, as
that mod gives them a use. If another mod brings other hidden skills back, switch off "1.2 skills list" (F12).

## Credits and rights

Mod by Grifm0le. The code and the translations into 15 languages were written with an AI assistant (Claude, Anthropic).
The original game's icons, pictures, texts and screen layouts belong to Battlestate Games.
The mod's code is under the MIT license (LICENSE.txt).

---

## Source and build

- `client/` — the BepInEx plugin (Interface10.dll). Build: `client\build.ps1 -Spt "<SPT folder>"` with Roslyn's
  `csc.exe`; the compiler options are in `client/build.rsp.template` (the same ones the release was built with).
- `server/` — the SPT server mod (Interface10.Server.dll): `dotnet build server/Interface10.Server.csproj -p:SptDir="<SPT folder>"`
  (.NET 10 SDK). The release was built with the same sources and references.
- `lang/` — the mod's texts in the game's 17 languages, installed next to the plugin as `lang/`.

## Not in this repository

`ui/` — the pictures the mod draws with (about 350 PNG: icons, frames, loading pictures and redrawn icons of EFT 1.0
and 1.2). They are Battlestate Games' art taken from the game, so they ship only in the release archive
(`BepInEx/plugins/Interface10/ui`). The complete mod is on its SPT Forge page.
