using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct StJobEventMessage
{
    // T_MaxString は STRING(255) なので 255 + 1 = 256
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Subject;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string JobNamespace;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string JobId;

    // STRING(16) なので 16 + 1 = 17
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)]
    public string OldState;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)]
    public string NewState;

    // UDINT は 符号なし32bit整数 (uint)
    public uint NumOfJob;

    // ULINT は 符号なし64bit整数 (ulong)
    public ulong RecordTime;
    
   // TwinCAT のリアルタイムクロックで生成された Windows NT Time (FileTime) を C# の DateTime に変換するプロパティ
    public DateTime RecordDateTime
    {
        get
        {
            try
            {
                // Windows NT Time (FileTime) から C# の DateTime (UTC) に変換し、ローカル時間に直す
                return DateTime.FromFileTimeUtc((long)RecordTime).ToLocalTime();
            }
            catch
            {
                // 万が一 PLC 側から不正な値（0や過大な値）が来た場合のセーフティ
                return DateTime.MinValue;
            }
        }
    }

    // ロガーや Console.WriteLine にこのオブジェクトが渡されたとき、自動的にこの文字列が使われます
    public override string ToString()
    {
        // yyyy-MM-dd HH:mm:ss.fff などの形式で出力
        string timeStr = RecordDateTime == DateTime.MinValue 
            ? "Invalid Time" 
            : RecordDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

        return $"Subject: {Subject}, Namespace: {JobNamespace}, JobId: {JobId}, " +
               $"OldState: {OldState}, NewState: {NewState}, NumOfJob: {NumOfJob}, RecordTime: {timeStr}";
    }
}

namespace TwinCAT.Ads.NcMapping
{
    // TwinCATのMC.NCTOPLC_AXIS_REF (256 bytes) と厳密にマッピングするための定義
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NcToPlcAxisRef
    {
        // -----------------------------------------------------------------
        // ステータス・エラー関連 (各4バイト / 計28バイト)
        // -----------------------------------------------------------------
        public uint StateDWord;            // 軸ステータス（ビットデータ）
        public uint ErrorCode;             // 軸エラーコード
        public uint AxisState;             // 軸の移動状態 (停止、加速、等速、減速など)
        public uint AxisModeConfirmation;  // 動作モード確認応答 (NCからのフィードバック)
        public uint HomingState;           // 原点復帰（キャリブレーション）ステータス
        public uint CoupleState;           // 軸の同期・結合ステータス
        public uint SvbEntries;            // SVB（設定準備タスク）のエントリ数

        // -----------------------------------------------------------------
        // 制御・指令値フィードバック (各8バイト / 計72バイト)
        // -----------------------------------------------------------------
        public double ActPos;              // 現在位置 (Actual Position)
        public double ModuloActPos;        // モジュロ現在位置
        public uint ActiveSetPosType;      // 有効な指令位置のタイプ (4バイト)
        private uint _pad0;                // アライメント用予約領域 (4バイト)
        public double SetPos;              // 指令位置 (Set Position)
        public double ModuloSetPos;        // モジュロ指令位置
        public double ModuloSetTurns;       // モジュロ指令回転数
        public double ActVelo;             // 現在速度 (Actual Velocity)
        public double SetVelo;             // 指令速度 (Set Velocity)
        public double SetAcc;              // 指令加速度 (Set Acceleration)
        public double SetJerk;             // 指令ジャーク (Set Jerk)

        // -----------------------------------------------------------------
        // トルク・その他フィードバック (各8バイト / 計48バイト)
        // -----------------------------------------------------------------
        public double ActTorque;           // 現在トルク (Actual Torque)
        public double SetTorque;           // 指令トルク (Set Torque)
        public double ActTorqueDerivate;   // 現在トルク微分値
        public double SetTorqueDerivate;   // 指令トルク微分値
        public double ActAcceleration;     // 現在加速度 (Actual Acceleration)
        public double PosDiff;             // 位置偏差 (Following Error / Position Lag)

        // -----------------------------------------------------------------
        // 内部ステータス・追加情報 (各4バイト / 計44バイト)
        // -----------------------------------------------------------------
        public uint OperationalState;      // 内部運用ステータス
        public uint AxisFilterState;       // 軸フィルタステータス
        public uint DynamicEmergencyStopState; // 動的非常停止ステータス
        public uint RawPositionIncr;       // 生のエンコーダ位置（インクリメント値）
        public uint RawPositionIncrSecondary; // セカンダリエンコーダ生のインクリメント値
        public uint SafEntries;            // SAFタスクのエントリ数
        public uint NcToPlcCount;          // NC-PLC間のサイクルカウンタ
        public uint TrajectoryState;       // 軌道生成ステータス
        public int ControlLoopState;       // 制御ループステータス (DINT -> int)
        public uint ExtensionState;        // 拡張ステータス
        public uint ReserveDWord1;         // 予約領域 1

        // -----------------------------------------------------------------
        // 予約領域 (計64バイト)
        // 256バイトの固定サイズを満たすためのパディングパケット
        // -----------------------------------------------------------------
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] Reserve;             // 構造体全体のサイズを256バイトに固定
    }
}