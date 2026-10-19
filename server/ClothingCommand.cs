using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Dialogue.Commando.SptCommands;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Dialog;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;

namespace Interface10Server;

/// <summary>
/// The Commando's "spt clothing on|off": Ragman's clothing of both sides (EFT 1.2's BEAR/USEC toggles, an Unheard Edition
/// feature) for a character of another edition too. Kept per profile in clothing-both-sides.json beside this mod; the
/// client asks GET /interface10/clothing/sides whether its character has it.
/// </summary>
[Injectable]
public class ClothingCommand(MailSendService mail, LocaleService locales) : ISptCommand
{
    private static readonly string File = Path.Combine(Path.GetDirectoryName(typeof(ClothingCommand).Assembly.Location) ?? ".", "clothing-both-sides.json");
    private static readonly object Lock = new();
    private static HashSet<string>? _profiles;

    public string Command => "clothing";

    public string CommandHelp =>
        "spt clothing\n========\nRagman shows the clothing of both sides (BEAR and USEC) to this character, as for the Unheard Edition.\n\n\tspt clothing on\n\tspt clothing off";

    public ValueTask<string> PerformAction(UserDialogInfo commandHandler, MongoId sessionId, SendMessageRequest request)
    {
        var words = (request.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var what = words.Length > 2 ? words[2].ToLowerInvariant() : "";
        string reply;
        if (what == "on" || what == "off")
        {
            Set(sessionId, what == "on");
            reply = Text(what);
        }
        else
        {
            reply = Text("usage") + "\n" + Text(Enabled(sessionId) ? "now_on" : "now_off");
        }

        mail.SendUserMessageToPlayer(sessionId, commandHandler, reply, [], null);
        return new ValueTask<string>(request.DialogId);
    }

    internal static bool Enabled(MongoId sessionId)
    {
        lock (Lock)
        {
            return Load().Contains(sessionId.ToString());
        }
    }

    private static void Set(MongoId sessionId, bool on)
    {
        lock (Lock)
        {
            var profiles = Load();
            var changed = on ? profiles.Add(sessionId.ToString()) : profiles.Remove(sessionId.ToString());
            if (changed)
            {
                System.IO.File.WriteAllText(File, JsonSerializer.Serialize(profiles.OrderBy(p => p).ToList(), new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }

    private static HashSet<string> Load()
    {
        if (_profiles != null)
        {
            return _profiles;
        }

        try
        {
            _profiles = System.IO.File.Exists(File)
                ? new HashSet<string>(JsonSerializer.Deserialize<List<string>>(System.IO.File.ReadAllText(File)) ?? [])
                : [];
        }
        catch (Exception)
        {
            _profiles = [];
        }

        return _profiles;
    }

    // The replies in the game's language (SPT's game locale), English when it has none of these.
    private string Text(string key)
    {
        var language = locales.GetDesiredGameLocale() ?? "en";
        if (!Texts.TryGetValue(language, out var texts) && !Texts.TryGetValue(language.Split('-')[0], out texts))
        {
            texts = Texts["en"];
        }

        return texts.TryGetValue(key, out var text) ? text : Texts["en"][key];
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["en"] = new()
        {
            ["on"] = "Ragman now shows the clothing of both sides, BEAR and USEC, to this character. \"spt clothing off\" turns it back.",
            ["off"] = "Ragman shows only your side's clothing again (the Unheard Edition always has both).",
            ["usage"] = "\"spt clothing on\": the clothing of both sides at Ragman, as with the Unheard Edition. \"spt clothing off\": your side's only.",
            ["now_on"] = "Now: both sides.",
            ["now_off"] = "Now: your side only.",
        },
        ["ru"] = new()
        {
            ["on"] = "Теперь Барахольщик показывает этому персонажу одежду обеих сторон, BEAR и USEC. \"spt clothing off\" вернёт как было.",
            ["off"] = "Барахольщик снова показывает одежду только вашей стороны (в издании Unheard обе стороны есть всегда).",
            ["usage"] = "\"spt clothing on\": одежда обеих сторон у Барахольщика, как в издании Unheard. \"spt clothing off\": только своей стороны.",
            ["now_on"] = "Сейчас: обе стороны.",
            ["now_off"] = "Сейчас: только своя сторона.",
        },
        ["ge"] = new()
        {
            ["on"] = "Ragman zeigt diesem Charakter jetzt die Kleidung beider Seiten, BEAR und USEC. \"spt clothing off\" macht es rückgängig.",
            ["off"] = "Ragman zeigt wieder nur die Kleidung deiner Seite (die Unheard Edition hat immer beide).",
            ["usage"] = "\"spt clothing on\": die Kleidung beider Seiten bei Ragman, wie mit der Unheard Edition. \"spt clothing off\": nur deine Seite.",
            ["now_on"] = "Jetzt: beide Seiten.",
            ["now_off"] = "Jetzt: nur deine Seite.",
        },
        ["fr"] = new()
        {
            ["on"] = "Ragman montre désormais à ce personnage les vêtements des deux camps, BEAR et USEC. « spt clothing off » revient en arrière.",
            ["off"] = "Ragman ne montre de nouveau que les vêtements de votre camp (l'édition Unheard a toujours les deux).",
            ["usage"] = "« spt clothing on » : les vêtements des deux camps chez Ragman, comme avec l'édition Unheard. « spt clothing off » : votre camp seulement.",
            ["now_on"] = "Actuellement : les deux camps.",
            ["now_off"] = "Actuellement : votre camp seulement.",
        },
        ["es"] = new()
        {
            ["on"] = "Ragman ahora muestra a este personaje la ropa de ambos bandos, BEAR y USEC. «spt clothing off» lo deshace.",
            ["off"] = "Ragman vuelve a mostrar solo la ropa de tu bando (la edición Unheard siempre tiene ambos).",
            ["usage"] = "«spt clothing on»: la ropa de ambos bandos en Ragman, como con la edición Unheard. «spt clothing off»: solo tu bando.",
            ["now_on"] = "Ahora: ambos bandos.",
            ["now_off"] = "Ahora: solo tu bando.",
        },
        ["es-mx"] = new()
        {
            ["on"] = "Ragman ahora le muestra a este personaje la ropa de ambos bandos, BEAR y USEC. «spt clothing off» lo deshace.",
            ["off"] = "Ragman vuelve a mostrar solo la ropa de tu bando (la edición Unheard siempre tiene ambos).",
            ["usage"] = "«spt clothing on»: la ropa de ambos bandos con Ragman, como con la edición Unheard. «spt clothing off»: solo tu bando.",
            ["now_on"] = "Ahora: ambos bandos.",
            ["now_off"] = "Ahora: solo tu bando.",
        },
        ["it"] = new()
        {
            ["on"] = "Ora Ragman mostra a questo personaggio i vestiti di entrambe le fazioni, BEAR e USEC. «spt clothing off» torna indietro.",
            ["off"] = "Ragman mostra di nuovo solo i vestiti della tua fazione (l'edizione Unheard ha sempre entrambe).",
            ["usage"] = "«spt clothing on»: i vestiti di entrambe le fazioni da Ragman, come con l'edizione Unheard. «spt clothing off»: solo la tua fazione.",
            ["now_on"] = "Ora: entrambe le fazioni.",
            ["now_off"] = "Ora: solo la tua fazione.",
        },
        ["pl"] = new()
        {
            ["on"] = "Ragman pokazuje teraz tej postaci ubrania obu stron, BEAR i USEC. „spt clothing off” przywraca poprzedni stan.",
            ["off"] = "Ragman znów pokazuje tylko ubrania twojej strony (edycja Unheard zawsze ma obie).",
            ["usage"] = "„spt clothing on”: ubrania obu stron u Ragmana, jak w edycji Unheard. „spt clothing off”: tylko twojej strony.",
            ["now_on"] = "Teraz: obie strony.",
            ["now_off"] = "Teraz: tylko twoja strona.",
        },
        ["cz"] = new()
        {
            ["on"] = "Ragman teď této postavě ukazuje oblečení obou stran, BEAR i USEC. „spt clothing off“ to vrátí zpět.",
            ["off"] = "Ragman opět ukazuje jen oblečení vaší strany (edice Unheard má vždy obě).",
            ["usage"] = "„spt clothing on“: oblečení obou stran u Ragmana, jako v edici Unheard. „spt clothing off“: jen vaší strany.",
            ["now_on"] = "Nyní: obě strany.",
            ["now_off"] = "Nyní: jen vaše strana.",
        },
        ["sk"] = new()
        {
            ["on"] = "Ragman teraz tejto postave ukazuje oblečenie oboch strán, BEAR aj USEC. „spt clothing off“ to vráti späť.",
            ["off"] = "Ragman opäť ukazuje len oblečenie vašej strany (edícia Unheard má vždy obe).",
            ["usage"] = "„spt clothing on“: oblečenie oboch strán u Ragmana, ako v edícii Unheard. „spt clothing off“: len vašej strany.",
            ["now_on"] = "Teraz: obe strany.",
            ["now_off"] = "Teraz: len vaša strana.",
        },
        ["hu"] = new()
        {
            ["on"] = "Ragman mostantól mindkét oldal ruháit mutatja ennek a karakternek, BEAR és USEC. A „spt clothing off” visszaállítja.",
            ["off"] = "Ragman újra csak a saját oldalad ruháit mutatja (az Unheard kiadásban mindig mindkettő megvan).",
            ["usage"] = "„spt clothing on”: mindkét oldal ruhái Ragmannél, mint az Unheard kiadásban. „spt clothing off”: csak a saját oldaladé.",
            ["now_on"] = "Most: mindkét oldal.",
            ["now_off"] = "Most: csak a saját oldalad.",
        },
        ["ro"] = new()
        {
            ["on"] = "Ragman îi arată acum acestui personaj hainele ambelor tabere, BEAR și USEC. „spt clothing off” revine.",
            ["off"] = "Ragman arată din nou doar hainele taberei tale (ediția Unheard le are mereu pe amândouă).",
            ["usage"] = "„spt clothing on”: hainele ambelor tabere la Ragman, ca în ediția Unheard. „spt clothing off”: doar ale taberei tale.",
            ["now_on"] = "Acum: ambele tabere.",
            ["now_off"] = "Acum: doar tabăra ta.",
        },
        ["po"] = new()
        {
            ["on"] = "Agora o Ragman mostra a este personagem as roupas dos dois lados, BEAR e USEC. “spt clothing off” desfaz.",
            ["off"] = "O Ragman volta a mostrar só as roupas do seu lado (a edição Unheard sempre tem os dois).",
            ["usage"] = "“spt clothing on”: as roupas dos dois lados no Ragman, como na edição Unheard. “spt clothing off”: só do seu lado.",
            ["now_on"] = "Agora: os dois lados.",
            ["now_off"] = "Agora: só o seu lado.",
        },
        ["tu"] = new()
        {
            ["on"] = "Ragman artık bu karaktere iki tarafın da kıyafetlerini, BEAR ve USEC, gösteriyor. “spt clothing off” geri alır.",
            ["off"] = "Ragman yine yalnızca kendi tarafının kıyafetlerini gösteriyor (Unheard sürümünde ikisi de her zaman var).",
            ["usage"] = "“spt clothing on”: Ragman'da iki tarafın kıyafetleri, Unheard sürümündeki gibi. “spt clothing off”: yalnızca kendi tarafın.",
            ["now_on"] = "Şu an: iki taraf.",
            ["now_off"] = "Şu an: yalnızca kendi tarafın.",
        },
        ["ch"] = new()
        {
            ["on"] = "现在 Ragman 会向这个角色展示双方阵营（BEAR 和 USEC）的服装。输入“spt clothing off”可恢复。",
            ["off"] = "Ragman 重新只展示你所属阵营的服装（Unheard 版本始终可见双方）。",
            ["usage"] = "“spt clothing on”：在 Ragman 处显示双方阵营的服装，与 Unheard 版本相同。“spt clothing off”：只显示己方。",
            ["now_on"] = "当前：双方阵营。",
            ["now_off"] = "当前：仅己方阵营。",
        },
        ["jp"] = new()
        {
            ["on"] = "Ragmanがこのキャラクターに両陣営（BEARとUSEC）の服を表示するようになりました。「spt clothing off」で元に戻せます。",
            ["off"] = "Ragmanは再び自陣営の服だけを表示します（Unheard Editionは常に両方です）。",
            ["usage"] = "「spt clothing on」：Unheard Editionと同じく、Ragmanで両陣営の服を表示。「spt clothing off」：自陣営のみ。",
            ["now_on"] = "現在：両陣営。",
            ["now_off"] = "現在：自陣営のみ。",
        },
        ["kr"] = new()
        {
            ["on"] = "이제 Ragman이 이 캐릭터에게 양 진영(BEAR와 USEC)의 의상을 보여 줍니다. \"spt clothing off\"로 되돌릴 수 있습니다.",
            ["off"] = "Ragman이 다시 자기 진영의 의상만 보여 줍니다(Unheard 에디션은 항상 양쪽 모두).",
            ["usage"] = "\"spt clothing on\": Unheard 에디션처럼 Ragman에서 양 진영의 의상. \"spt clothing off\": 자기 진영만.",
            ["now_on"] = "현재: 양 진영.",
            ["now_off"] = "현재: 자기 진영만.",
        },
    };
}

/// <summary>GET /interface10/clothing/sides: { "both": true } when the player turned the clothing of both sides on with
/// "spt clothing on" (the client knows its edition itself).</summary>
[Injectable]
public class ClothingSidesRouter(JsonUtil jsonUtil) : StaticRouter(jsonUtil,
[
    new RouteAction<EmptyRequestData>("/interface10/clothing/sides",
        (url, info, sessionId, output, cancellationToken) =>
            new ValueTask<string>(ClothingCommand.Enabled(sessionId) ? "{\"both\":true}" : "{\"both\":false}")),
])
{
}
