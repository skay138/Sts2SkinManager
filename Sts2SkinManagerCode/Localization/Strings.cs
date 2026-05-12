using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Localization;

namespace Sts2SkinManager.Localization;

public static class Strings
{
    public static string Get(string key)
    {
        var lang = GetCurrentLanguage();
        if (_tables.TryGetValue(lang, out var table) && table.TryGetValue(key, out var value)) return value;
        if (_tables.TryGetValue("ENG", out var eng) && eng.TryGetValue(key, out var engValue)) return engValue;
        return key;
    }

    public static string Get(string key, params object[] args)
    {
        var raw = Get(key);
        try { return args.Length == 0 ? raw : string.Format(raw, args); }
        catch { return raw; }
    }

    private static string GetCurrentLanguage()
    {
        try { return (LocManager.Instance?.Language ?? "ENG").ToUpperInvariant(); }
        catch { return "ENG"; }
    }

    private static readonly Dictionary<string, Dictionary<string, string>> _tables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ENG"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "No detected skins",
            ["not_configured"] = "No configuration",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "To change the skin, STS2 must restart.\nChoose: restart now, or cancel the change.\n\nAuto-restart in {0}s via Steam.",
            ["btn_restart_now"] = "Restart now",
            ["btn_restart_later"] = "Restart later",
            ["card_packs_header"] = "Card skins",
            ["save_changes"] = "Save",
            ["discard_changes"] = "Discard",
        },
        ["KOR"] = new()
        {
            ["skin_label"] = "스킨",
            ["no_variants"] = "감지된 스킨 목록 없음",
            ["not_configured"] = "설정 없음",
            ["modal_title"] = "Sts2 스킨 매니저",
            ["modal_body"] = "스킨을 변경하려면 게임을 재시작해야 해요.\n즉시 재시작할지 변경을 취소할지 선택해주세요.\n\n{0}초 뒤 자동으로 재시작됩니다.",
            ["btn_restart_now"] = "지금 재시작",
            ["btn_restart_later"] = "나중에 재시작",
            ["card_packs_header"] = "카드 스킨",
            ["save_changes"] = "저장",
            ["discard_changes"] = "되돌리기",
        },
        ["JPN"] = new()
        {
            ["skin_label"] = "スキン",
            ["no_variants"] = "検出されたスキンなし",
            ["not_configured"] = "設定なし",
            ["modal_title"] = "Sts2 スキンマネージャー",
            ["modal_body"] = "スキンを変更するにはSTS2の再起動が必要です。\nすぐに再起動するか、変更を取り消すか選んでください。\n\n{0}秒後にSteamで自動再起動します。",
            ["btn_restart_now"] = "今すぐ再起動",
            ["btn_restart_later"] = "後で再起動",
            ["card_packs_header"] = "カードスキン",
            ["save_changes"] = "保存",
            ["discard_changes"] = "破棄",
        },
        ["ZHS"] = new()
        {
            ["skin_label"] = "皮肤",
            ["no_variants"] = "未检测到皮肤",
            ["not_configured"] = "无配置",
            ["modal_title"] = "Sts2 皮肤管理器",
            ["modal_body"] = "要更换皮肤,需要重启 STS2。\n请选择立即重启或取消更改。\n\n将在 {0} 秒后通过 Steam 自动重启。",
            ["btn_restart_now"] = "立即重启",
            ["btn_restart_later"] = "稍后重启",
            ["card_packs_header"] = "卡牌外观",
            ["save_changes"] = "保存",
            ["discard_changes"] = "丢弃",
        },
        ["ZHT"] = new()
        {
            ["skin_label"] = "皮膚",
            ["no_variants"] = "未偵測到皮膚",
            ["not_configured"] = "無配置",
            ["modal_title"] = "Sts2 皮膚管理器",
            ["modal_body"] = "要更換皮膚,需要重啟 STS2。\n請選擇立即重啟或取消變更。\n\n將在 {0} 秒後透過 Steam 自動重啟。",
            ["btn_restart_now"] = "立即重啟",
            ["btn_restart_later"] = "稍後重啟",
            ["card_packs_header"] = "卡牌外觀",
            ["save_changes"] = "儲存",
            ["discard_changes"] = "捨棄",
        },
        ["DEU"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "Keine erkannten Skins",
            ["not_configured"] = "Keine Konfiguration",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Um den Skin zu ändern, muss STS2 neu starten.\nWähle: jetzt neu starten oder Änderung abbrechen.\n\nAutomatischer Neustart in {0}s über Steam.",
            ["btn_restart_now"] = "Jetzt neu starten",
            ["btn_restart_later"] = "Später neu starten",
            ["card_packs_header"] = "Karten-Skins",
            ["save_changes"] = "Speichern",
            ["discard_changes"] = "Verwerfen",
        },
        ["FRA"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "Aucun skin détecté",
            ["not_configured"] = "Aucune configuration",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Pour changer de skin, STS2 doit redémarrer.\nChoisissez : redémarrer maintenant ou annuler le changement.\n\nRedémarrage automatique dans {0}s via Steam.",
            ["btn_restart_now"] = "Redémarrer maintenant",
            ["btn_restart_later"] = "Redémarrer plus tard",
            ["card_packs_header"] = "Skins de cartes",
            ["save_changes"] = "Enregistrer",
            ["discard_changes"] = "Annuler",
        },
        ["SPA"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "Ningún skin detectado",
            ["not_configured"] = "Sin configuración",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Para cambiar el skin, STS2 debe reiniciarse.\nElige: reiniciar ahora o cancelar el cambio.\n\nReinicio automático en {0}s vía Steam.",
            ["btn_restart_now"] = "Reiniciar ahora",
            ["btn_restart_later"] = "Reiniciar después",
            ["card_packs_header"] = "Diseños de cartas",
            ["save_changes"] = "Guardar",
            ["discard_changes"] = "Descartar",
        },
        ["ESP"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "Ningún skin detectado",
            ["not_configured"] = "Sin configuración",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Para cambiar el skin, STS2 debe reiniciarse.\nElige: reiniciar ahora o cancelar el cambio.\n\nReinicio automático en {0}s vía Steam.",
            ["btn_restart_now"] = "Reiniciar ahora",
            ["btn_restart_later"] = "Reiniciar después",
            ["card_packs_header"] = "Skins de cartas",
            ["save_changes"] = "Guardar",
            ["discard_changes"] = "Descartar",
        },
        ["ITA"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "Nessuna skin rilevata",
            ["not_configured"] = "Nessuna configurazione",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Per cambiare la skin, STS2 deve riavviarsi.\nScegli: riavvia ora o annulla la modifica.\n\nRiavvio automatico tra {0}s tramite Steam.",
            ["btn_restart_now"] = "Riavvia ora",
            ["btn_restart_later"] = "Riavvia dopo",
            ["card_packs_header"] = "Skin delle carte",
            ["save_changes"] = "Salva",
            ["discard_changes"] = "Annulla",
        },
        ["PTB"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "Nenhuma skin detectada",
            ["not_configured"] = "Sem configuração",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Para mudar a skin, o STS2 precisa reiniciar.\nEscolha: reiniciar agora ou cancelar a mudança.\n\nReinício automático em {0}s via Steam.",
            ["btn_restart_now"] = "Reiniciar agora",
            ["btn_restart_later"] = "Reiniciar depois",
            ["card_packs_header"] = "Skins de cartas",
            ["save_changes"] = "Salvar",
            ["discard_changes"] = "Descartar",
        },
        ["POR"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "Nenhuma skin detetada",
            ["not_configured"] = "Sem configuração",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Para alterar a skin, o STS2 precisa de reiniciar.\nEscolha: reiniciar agora ou cancelar a alteração.\n\nReinício automático em {0}s através do Steam.",
            ["btn_restart_now"] = "Reiniciar agora",
            ["btn_restart_later"] = "Reiniciar mais tarde",
            ["card_packs_header"] = "Skins de cartas",
            ["save_changes"] = "Guardar",
            ["discard_changes"] = "Descartar",
        },
        ["POL"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "Nie wykryto skinów",
            ["not_configured"] = "Brak konfiguracji",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Aby zmienić skina, STS2 musi się zrestartować.\nWybierz: zrestartuj teraz lub anuluj zmianę.\n\nAutomatyczny restart za {0}s przez Steam.",
            ["btn_restart_now"] = "Zrestartuj teraz",
            ["btn_restart_later"] = "Zrestartuj później",
            ["card_packs_header"] = "Skiny kart",
            ["save_changes"] = "Zapisz",
            ["discard_changes"] = "Odrzuć",
        },
        ["RUS"] = new()
        {
            ["skin_label"] = "Скин",
            ["no_variants"] = "Скины не обнаружены",
            ["not_configured"] = "Без конфигурации",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Чтобы изменить скин, STS2 должен перезапуститься.\nВыберите: перезапустить сейчас или отменить изменение.\n\nАвтоматический перезапуск через {0}с через Steam.",
            ["btn_restart_now"] = "Перезапустить сейчас",
            ["btn_restart_later"] = "Перезапустить позже",
            ["card_packs_header"] = "Скины карт",
            ["save_changes"] = "Сохранить",
            ["discard_changes"] = "Отмена",
        },
        ["THA"] = new()
        {
            ["skin_label"] = "สกิน",
            ["no_variants"] = "ไม่พบสกิน",
            ["not_configured"] = "ไม่มีการตั้งค่า",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "การเปลี่ยนสกินต้องรีสตาร์ท STS2\nเลือก: รีสตาร์ทตอนนี้ หรือยกเลิกการเปลี่ยนแปลง\n\nรีสตาร์ทอัตโนมัติใน {0} วินาที ผ่าน Steam",
            ["btn_restart_now"] = "รีสตาร์ทตอนนี้",
            ["btn_restart_later"] = "รีสตาร์ทภายหลัง",
            ["card_packs_header"] = "สกินการ์ด",
            ["save_changes"] = "บันทึก",
            ["discard_changes"] = "ยกเลิก",
        },
        ["TUR"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "Skin algılanmadı",
            ["not_configured"] = "Yapılandırma yok",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Skini değiştirmek için STS2'nin yeniden başlatılması gerekir.\nSeçin: şimdi yeniden başlat veya değişikliği iptal et.\n\n{0}s sonra Steam ile otomatik yeniden başlatma.",
            ["btn_restart_now"] = "Şimdi yeniden başlat",
            ["btn_restart_later"] = "Sonra yeniden başlat",
            ["card_packs_header"] = "Kart skinleri",
            ["save_changes"] = "Kaydet",
            ["discard_changes"] = "İptal",
        },
    };
}
