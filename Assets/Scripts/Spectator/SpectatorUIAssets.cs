using System;
using UnityEngine;

public enum SpectatorTeamLayout { BlueVersusOthers, ProportionalTeams, EqualTeams }

[CreateAssetMenu(menuName = "Unite/Spectator UI Assets", fileName = "SpectatorUIAssets")]
public class SpectatorUIAssets : ScriptableObject
{
    public SpectatorBattleHUD battleHUD;
    public SpectatorSelectionOverlay selectionOverlay;
    public SpectatorPlayerCard playerCard;
    public SpectatorSelectionCard selectionCard;
    public SpectatorTeamPanel teamPanel;
    public DraftBanHolder banSlot;
    public LobbyPlayerIcon lobbySlot;
    public RectTransform lobbySlots;
    public Sprite blueCardBackground;
    public Sprite orangeCardBackground;
    [Tooltip("Monochrome player-card background, tinted with the displayed team's color for all teams.")]
    public Sprite neutralCardBackground;
    public Color blueColor = new Color(0.43f, 0.2f, 0.91f);
    public Color orangeColor = new Color(1f, 0.49f, 0.04f);
    public TeamTint[] teamColors = Array.Empty<TeamTint>();

    private static SpectatorUIAssets instance;
    public static SpectatorUIAssets Instance => instance != null ? instance : instance = Resources.Load<SpectatorUIAssets>("Spectator/SpectatorUIAssets");
    public static bool IsSpectating => LobbyController.Instance != null && LobbyController.Instance.IsLocalSpectator;

    public Color GetColor(Team team, SpectatorTeamLayout layout)
    {
        if (team == Team.Blue) return blueColor;
        if (layout == SpectatorTeamLayout.BlueVersusOthers || team == Team.Orange) return orangeColor;
        foreach (var tint in teamColors) if (tint.team == team) return tint.color;
        return Color.HSVToRGB(((int)team * 0.137f) % 1f, 0.65f, 0.95f);
    }

    public Sprite GetBackground(Team team) => neutralCardBackground != null ? neutralCardBackground :
        team == Team.Blue ? blueCardBackground : orangeCardBackground;
    public Sprite GetBackground(Team team, SpectatorTeamLayout layout)
    {
        if (layout == SpectatorTeamLayout.BlueVersusOthers) team = team == Team.Blue ? Team.Blue : Team.Orange;
        foreach (var tint in teamColors) if (tint.team == team && tint.background != null) return tint.background;
        return GetBackground(team);
    }

    [Serializable]
    public struct TeamTint { public Team team; public Color color; public Sprite background; }
}
