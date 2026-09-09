namespace Marea.Economy
{
    /// <summary>
    /// 업그레이드할 수 있는 시설의 종류. 계약 8이다 (2차 분담).
    ///
    /// FacilityLevels.cs가 아니라 따로 둔 이유: FacilityData(SO)가 이걸 참조하는데,
    /// 아직 FacilityLevels가 없어서다. 항목을 늘리는 건 A 마음대로 해도 되지만
    /// (계약 8 명시), 지우거나 이름을 바꾸는 건 B에게 먼저 말한다.
    /// </summary>
    public enum FacilityKind
    {
        Business,
        Kitchen,
        Storage,
    }
}
