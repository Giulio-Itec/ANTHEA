using System.Text.Json.Nodes;
using X.Core;
using X.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Length==3&&args[0]=="--smoke")
        {
            var form=new MainForm();form.Shown+=async(_,_)=>{try{await form.Smoke(args[1],JsonNode.Parse(File.ReadAllText(args[2]))!.AsArray());}catch(Exception ex){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"errore.txt"),ex.ToString());Environment.ExitCode=1;}finally{form.Close();}};Application.Run(form);return;
        }
        Application.ThreadException+=(_,e)=>MessageBox.Show(e.Exception.Message,"ANTHEA — errore",MessageBoxButtons.OK,MessageBoxIcon.Error);
        Application.Run(new MainForm(args.FirstOrDefault(a=>!a.StartsWith("--"))));
    }
}
