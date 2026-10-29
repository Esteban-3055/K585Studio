using System.Net;
using System.Diagnostics;
using System.Reflection;
using K585Studio.Core;

namespace K585Studio.App;

internal sealed partial class MainForm
{
    private void RunUpdaterUiChecks(List<string> checks)
    {
        void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);checks.Add(label);}
        void Await(Task task)
        {
            var timer=Stopwatch.StartNew();
            while(!task.IsCompleted){if(timer.Elapsed>TimeSpan.FromSeconds(10))throw new TimeoutException("Updater UI check did not finish.");Application.DoEvents();Thread.Sleep(1);}
            task.GetAwaiter().GetResult();
        }
        var closed=typeof(ToolStripDropDown).GetMethod("OnClosed",BindingFlags.Instance|BindingFlags.NonPublic)!;
        // Reproduce the event ordering in the actual Windows crash stack, with
        // no displayed menus, input injection or changes to the user's window.
        using(var old=new ContextMenuStrip())
        {
            old.Closed+=(_,_)=>old.Dispose();closed.Invoke(old,[new ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason.AppClicked)]);
            bool reproduced=false;try{_=old.Handle;}catch(ObjectDisposedException){reproduced=true;}
            Check(reproduced,"Regression harness reproduces disposed menu handle failure");
        }
        var menu=Menu();menu.Items.Add("Efecto de prueba");
        closed.Invoke(menu,[new ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason.AppClicked)]);
        Check(!menu.IsDisposed&&menu.Handle!=IntPtr.Zero,"Closed menu stays valid while WinForms finishes processing the click");
        var reused=Menu();Check(ReferenceEquals(menu,reused)&&reused.Items.Count==0,"Form owns and reuses one menu without accumulating items");
        HandleCommand("updates").GetAwaiter().GetResult();Check(Page==StudioPage.Updates,"Navigation to updater works after closing a menu");
        HttpStatusCode status=HttpStatusCode.Forbidden;string body="{}";
        using var probe=new MainForm(false,new HttpClient(new UiUpdateHttp(_=>new(status){Content=new StringContent(body)})));
        probe.HandleCommand("update-check").GetAwaiter().GetResult();
        Check(probe.Error&&!probe.checkingUpdates&&probe.UpdateStatus.StartsWith("No se pudo comprobar:"),"GitHub HTTP failure stays inside updater without a crash");
        status=HttpStatusCode.OK;body="invalid JSON";probe.HandleCommand("update-check").GetAwaiter().GetResult();
        Check(probe.Error&&!probe.checkingUpdates,"Invalid release response is handled and updater can retry");
        status=HttpStatusCode.NotFound;probe.HandleCommand("update-check").GetAwaiter().GetResult();
        Check(!probe.Error&&probe.AvailableUpdate==null&&!probe.checkingUpdates,"Retry succeeds after error and clears stale error state");
        Await(probe.HandleCommand("updates-firmware"));
        Check(probe.FirmwareTab&&probe.Page==StudioPage.Updates,"Firmware tab uses ordinary command routing without opening USB");
        Check(probe.LoadedFirmware?.UpdaterSha256==Firmware.BundledUpdaterSha256&&probe.PreparedFirmware==null,"Firmware tab verifies the embedded updater without creating an executable or backup");
        string originalStatus=probe.FirmwareStatus;Await(probe.HandleCommand("updates-firmware"));
        Check(probe.FirmwareStatus==originalStatus&&!probe.Busy,"Returning to firmware tab reuses the validated in-memory source");
    }
    private sealed class UiUpdateHttp(Func<HttpRequestMessage,HttpResponseMessage> reply):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {ct.ThrowIfCancellationRequested();var response=reply(request);response.RequestMessage=request;return Task.FromResult(response);}
    }
}
