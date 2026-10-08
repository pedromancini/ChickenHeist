# Sends real keystrokes to the focused game window for InputProbeReview:
# walk + jump, then sprint (Left Shift) + jump, three times each.
Add-Type @"
using System;using System.Runtime.InteropServices;
public static class Keys {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern IntPtr FindWindow(string cls, string title);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
}
"@
function Down($vk,$scan){[Keys]::keybd_event($vk,$scan,0x0008,[UIntPtr]::Zero)}
function Up($vk,$scan){[Keys]::keybd_event($vk,$scan,0x0008 -bor 0x0002,[UIntPtr]::Zero)}
$W=@(0x57,0x11);$SHIFT=@(0xA0,0x2A);$SPACE=@(0x20,0x39)
$hwnd=[Keys]::FindWindow($null,"ChickenHeist");if($hwnd -ne [IntPtr]::Zero){[Keys]::SetForegroundWindow($hwnd)|Out-Null}
Start-Sleep -Milliseconds 800
for($i=0;$i -lt 3;$i++){
  Down @W; Start-Sleep -Milliseconds 700; Down @SPACE; Start-Sleep -Milliseconds 80; Up @SPACE; Start-Sleep -Milliseconds 900; Up @W
  Start-Sleep -Milliseconds 600
  Down @W; Down @SHIFT; Start-Sleep -Milliseconds 900; Down @SPACE; Start-Sleep -Milliseconds 80; Up @SPACE; Start-Sleep -Milliseconds 900; Up @SHIFT; Up @W
  Start-Sleep -Milliseconds 600
}
"sent"
