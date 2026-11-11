// 레이어 번호는 기초데이터가 원본이고 TagManager.asset과 함께 맞춘다.
// Physics 질의에는 번호에서 파생한 비트마스크를 사용한다.
public static class GameLayers
{
    // 레이어 번호(TagManager.asset 순서 기준, 0부터 시작).
    public static int GroundLayerNumber => BasicData.Int("layers.GameLayers.GroundLayerNumber");
    public static int ObstacleLayerNumber => BasicData.Int("layers.GameLayers.ObstacleLayerNumber");
    public static int PlayerLayerNumber => BasicData.Int("layers.GameLayers.PlayerLayerNumber");
    public static int EnemyLayerNumber => BasicData.Int("layers.GameLayers.EnemyLayerNumber");
    public static int PropLayerNumber => BasicData.Int("layers.GameLayers.PropLayerNumber");
    public static int WeaponLayerNumber => BasicData.Int("layers.GameLayers.WeaponLayerNumber");
    public static int ProjectileLayerNumber => BasicData.Int("layers.GameLayers.ProjectileLayerNumber");

    // Physics 질의에 쓰는 비트마스크. 기초데이터의 번호에서 파생한다.
    public static int GroundMask => 1 << GroundLayerNumber;
    public static int ObstacleMask => 1 << ObstacleLayerNumber;
    public static int PlayerMask => 1 << PlayerLayerNumber;
    public static int EnemyMask => 1 << EnemyLayerNumber;
    public static int PropMask => 1 << PropLayerNumber;
    public static int WeaponMask => 1 << WeaponLayerNumber;
    public static int ProjectileMask => 1 << ProjectileLayerNumber;

    // 자주 쓰는 결합 질의. 개별 질의마다 즉석 마스크를 만들지 않는다.
    public static int GroundPlayerMask => GroundMask | PlayerMask;
    public static int GroundObstacleMask => GroundMask | ObstacleMask;
    public static int CharacterMask => PlayerMask | EnemyMask;
    public static int WorldMask => GroundMask | ObstacleMask | PropMask;

    // Validate Project가 TagManager.asset의 이름과 대조하는 표. 번호를 바꾸면 여기도 함께 고친다.
    public static (int number, string name)[] TagManagerNames => new (int number, string name)[]
    {
        (GroundLayerNumber, "Ground"),
        (ObstacleLayerNumber, "Obstacle"),
        (PlayerLayerNumber, "Player"),
        (EnemyLayerNumber, "Enemy"),
        (PropLayerNumber, "Prop"),
        (WeaponLayerNumber, "Weapon"),
        (ProjectileLayerNumber, "Projectile"),
    };
}
