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
            ["no_variants"] = "(no variants)",
            ["not_configured"] = "(not configured)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Your skin choice was saved.\n\nTo see the visual change, STS2 must restart.\nAuto-restart in {0}s via Steam.\n\n• Restart now → apply immediately\n• Restart later → keep playing; choice reverts on cancel.",
            ["btn_restart_now"] = "Restart now",
            ["btn_restart_later"] = "Restart later",
        },
        ["KOR"] = new()
        {
            ["skin_label"] = "스킨",
            ["no_variants"] = "(변형 없음)",
            ["not_configured"] = "(설정 안 됨)",
            ["modal_title"] = "Sts2 스킨 매니저",
            ["modal_body"] = "스킨 선택이 저장됐어요.\n\n시각 변경을 보려면 STS2 재시작이 필요합니다.\nSteam 으로 {0}초 후 자동 재시작.\n\n• 지금 재시작 → 즉시 적용\n• 나중에 재시작 → 계속 플레이, 취소 시 선택 되돌림",
            ["btn_restart_now"] = "지금 재시작",
            ["btn_restart_later"] = "나중에 재시작",
        },
        ["JPN"] = new()
        {
            ["skin_label"] = "スキン",
            ["no_variants"] = "(バリエーションなし)",
            ["not_configured"] = "(未設定)",
            ["modal_title"] = "Sts2 スキンマネージャー",
            ["modal_body"] = "スキンの選択を保存しました。\n\n反映するにはSTS2の再起動が必要です。\nSteamで{0}秒後に自動再起動します。\n\n• 今すぐ再起動 → 即時適用\n• 後で再起動 → プレイを続ける、キャンセル時は選択を戻します",
            ["btn_restart_now"] = "今すぐ再起動",
            ["btn_restart_later"] = "後で再起動",
        },
        ["ZHS"] = new()
        {
            ["skin_label"] = "皮肤",
            ["no_variants"] = "(无变体)",
            ["not_configured"] = "(未配置)",
            ["modal_title"] = "Sts2 皮肤管理器",
            ["modal_body"] = "皮肤选择已保存。\n\n要应用视觉变化,需要重启 STS2。\n将在 {0} 秒后通过 Steam 自动重启。\n\n• 立即重启 → 立即应用\n• 稍后重启 → 继续游戏,取消时还原选择",
            ["btn_restart_now"] = "立即重启",
            ["btn_restart_later"] = "稍后重启",
        },
        ["ZHT"] = new()
        {
            ["skin_label"] = "皮膚",
            ["no_variants"] = "(無變體)",
            ["not_configured"] = "(未配置)",
            ["modal_title"] = "Sts2 皮膚管理器",
            ["modal_body"] = "皮膚選擇已儲存。\n\n要套用視覺變化,需要重啟 STS2。\n將在 {0} 秒後透過 Steam 自動重啟。\n\n• 立即重啟 → 立即套用\n• 稍後重啟 → 繼續遊戲,取消時還原選擇",
            ["btn_restart_now"] = "立即重啟",
            ["btn_restart_later"] = "稍後重啟",
        },
        ["DEU"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "(keine Varianten)",
            ["not_configured"] = "(nicht konfiguriert)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Deine Skin-Auswahl wurde gespeichert.\n\nFür die visuelle Änderung muss STS2 neu starten.\nAutomatischer Neustart in {0}s über Steam.\n\n• Jetzt neu starten → sofort anwenden\n• Später neu starten → weiter spielen; Auswahl wird bei Abbruch zurückgesetzt",
            ["btn_restart_now"] = "Jetzt neu starten",
            ["btn_restart_later"] = "Später neu starten",
        },
        ["FRA"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "(aucune variante)",
            ["not_configured"] = "(non configuré)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Votre choix de skin a été enregistré.\n\nPour voir le changement visuel, STS2 doit redémarrer.\nRedémarrage automatique dans {0}s via Steam.\n\n• Redémarrer maintenant → appliquer immédiatement\n• Redémarrer plus tard → continuer à jouer ; choix annulé si refusé",
            ["btn_restart_now"] = "Redémarrer maintenant",
            ["btn_restart_later"] = "Redémarrer plus tard",
        },
        ["SPA"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "(sin variantes)",
            ["not_configured"] = "(no configurado)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Tu elección de skin se ha guardado.\n\nPara ver el cambio visual, STS2 debe reiniciarse.\nReinicio automático en {0}s vía Steam.\n\n• Reiniciar ahora → aplicar inmediatamente\n• Reiniciar después → seguir jugando; la elección se revierte al cancelar",
            ["btn_restart_now"] = "Reiniciar ahora",
            ["btn_restart_later"] = "Reiniciar después",
        },
        ["ESP"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "(sin variantes)",
            ["not_configured"] = "(no configurado)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Tu elección de skin se ha guardado.\n\nPara ver el cambio visual, STS2 debe reiniciarse.\nReinicio automático en {0}s vía Steam.\n\n• Reiniciar ahora → aplicar inmediatamente\n• Reiniciar después → seguir jugando; la elección se revierte al cancelar",
            ["btn_restart_now"] = "Reiniciar ahora",
            ["btn_restart_later"] = "Reiniciar después",
        },
        ["ITA"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "(nessuna variante)",
            ["not_configured"] = "(non configurato)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "La tua scelta di skin è stata salvata.\n\nPer vedere il cambio visivo, STS2 deve riavviarsi.\nRiavvio automatico tra {0}s tramite Steam.\n\n• Riavvia ora → applica immediatamente\n• Riavvia dopo → continua a giocare; scelta annullata se rifiuti",
            ["btn_restart_now"] = "Riavvia ora",
            ["btn_restart_later"] = "Riavvia dopo",
        },
        ["PTB"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "(sem variantes)",
            ["not_configured"] = "(não configurado)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Sua escolha de skin foi salva.\n\nPara ver a mudança visual, STS2 precisa reiniciar.\nReinício automático em {0}s via Steam.\n\n• Reiniciar agora → aplicar imediatamente\n• Reiniciar depois → continuar jogando; escolha revertida ao cancelar",
            ["btn_restart_now"] = "Reiniciar agora",
            ["btn_restart_later"] = "Reiniciar depois",
        },
        ["POR"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "(sem variantes)",
            ["not_configured"] = "(não configurado)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "A sua escolha de skin foi guardada.\n\nPara ver a alteração visual, o STS2 precisa de reiniciar.\nReinício automático em {0}s através do Steam.\n\n• Reiniciar agora → aplicar imediatamente\n• Reiniciar mais tarde → continuar a jogar; escolha revertida ao cancelar",
            ["btn_restart_now"] = "Reiniciar agora",
            ["btn_restart_later"] = "Reiniciar mais tarde",
        },
        ["POL"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "(brak wariantów)",
            ["not_configured"] = "(nieskonfigurowany)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Twój wybór skina został zapisany.\n\nAby zobaczyć zmianę wizualną, STS2 musi się zrestartować.\nAutomatyczny restart za {0}s przez Steam.\n\n• Zrestartuj teraz → zastosuj natychmiast\n• Zrestartuj później → kontynuuj grę; wybór zostanie cofnięty po anulowaniu",
            ["btn_restart_now"] = "Zrestartuj teraz",
            ["btn_restart_later"] = "Zrestartuj później",
        },
        ["RUS"] = new()
        {
            ["skin_label"] = "Скин",
            ["no_variants"] = "(нет вариантов)",
            ["not_configured"] = "(не настроено)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Ваш выбор скина сохранён.\n\nДля визуального изменения STS2 должен перезапуститься.\nАвтоматический перезапуск через {0}с через Steam.\n\n• Перезапустить сейчас → применить немедленно\n• Перезапустить позже → продолжить игру; выбор сбросится при отмене",
            ["btn_restart_now"] = "Перезапустить сейчас",
            ["btn_restart_later"] = "Перезапустить позже",
        },
        ["THA"] = new()
        {
            ["skin_label"] = "สกิน",
            ["no_variants"] = "(ไม่มีตัวเลือก)",
            ["not_configured"] = "(ไม่ได้กำหนดค่า)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "การเลือกสกินของคุณถูกบันทึก\n\nเพื่อให้เห็นการเปลี่ยนแปลง STS2 ต้องรีสตาร์ท\nรีสตาร์ทอัตโนมัติใน {0} วินาที ผ่าน Steam\n\n• รีสตาร์ทตอนนี้ → ใช้งานทันที\n• รีสตาร์ทภายหลัง → เล่นต่อ; ยกเลิกจะคืนค่าการเลือก",
            ["btn_restart_now"] = "รีสตาร์ทตอนนี้",
            ["btn_restart_later"] = "รีสตาร์ทภายหลัง",
        },
        ["TUR"] = new()
        {
            ["skin_label"] = "Skin",
            ["no_variants"] = "(varyant yok)",
            ["not_configured"] = "(yapılandırılmamış)",
            ["modal_title"] = "Sts2 Skin Manager",
            ["modal_body"] = "Skin seçiminiz kaydedildi.\n\nGörsel değişikliği görmek için STS2 yeniden başlatılmalı.\n{0}s sonra Steam ile otomatik yeniden başlatma.\n\n• Şimdi yeniden başlat → hemen uygula\n• Sonra yeniden başlat → oynamaya devam et; iptal edilirse seçim geri alınır",
            ["btn_restart_now"] = "Şimdi yeniden başlat",
            ["btn_restart_later"] = "Sonra yeniden başlat",
        },
    };
}
