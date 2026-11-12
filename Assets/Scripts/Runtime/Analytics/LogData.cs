using System;

// 분석 로그(JSONL)의 운영 설정. 원본은 Assets/Resources/LogData.json이며 도메인 테이블과 분리한다.
// 테이블이 아닌 이유: 게임 규칙이 아니라 로그 기록 체서의 동작 스위치이고, LogKit 측 전송 설정과 짝을 이루는 운영 자료다.
// 서버 주소·업로드 키는 여기에 두지 않는다. 전송 설정은 LogKit(LogKitSetup·빌드 환경변수)이 원본이다.
[Serializable]
public sealed class LogData
{
    // 이 파일의 목적을 적어 두는 자리. 동작에는 영향이 없다.
    public string note;
    // false면 분석 원본 수집·로컬 보관·Data 전송을 끈다. 콘솔·오류 자동 수집에는 영향이 없다.
    public bool enabled;
    // 디스크에 쓰는 주기(줄 수). 1이면 한 줄 쓸 때마다 저장하고, 크게 하면 종료 직전에 몰아 쓴다.
    public int bufferLines;
    // 이 값보다 오래된 로컬 분석 보관본을 기동에 정리한다. LogKit 전송 대기 파일은 별도다.
    public int retentionDays;
}
