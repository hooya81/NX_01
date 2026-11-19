using System;

namespace NxOpenFile.NxBridge
{
    /// <summary>
    /// EXE에서 호출할 수 있는 NX 명령 함수에 붙입니다.
    ///
    /// 함수 형식 (반드시 이 형식):
    ///   [NxCommand("명령이름")]
    ///   public static string 함수이름(Session session, string arg)
    ///
    /// - session: 브리지가 넣어 주는 현재 NX 세션 (NX 메인 스레드에서 실행됨)
    /// - arg:     EXE가 보낸 인수 (없으면 빈 문자열)
    /// - 반환값:  EXE로 돌려줄 문자열 (여러 줄 가능)
    /// - 실패 시: 예외를 던지면 EXE에서 BridgeException(메시지)으로 받습니다.
    ///
    /// 이름을 생략하면 함수 이름이 명령 이름이 됩니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class NxCommandAttribute : Attribute
    {
        public NxCommandAttribute()
        {
        }

        public NxCommandAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }
    }
}
