// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Forms;
internal static class HallSceneTests
{
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    private static Delegate NullGetter(Type valueType){Type type=typeof(Func<>).MakeGenericType(valueType);return Expression.Lambda(type,Expression.Constant(null,valueType)).Compile();}
    [STAThread]private static int Main(string[] args)
    {
        try
        {
            Require(Environment.GetEnvironmentVariable("FUFU_TEST_MODE")=="1"&&AppDomain.CurrentDomain.BaseDirectory.Contains("xianpal-hall-install-test-"),"isolated installed runtime required");
            Application.EnableVisualStyles();
            string app=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"OrbitPet.exe");Assembly assembly=Assembly.LoadFrom(app);
            using(Stream stream=assembly.GetManifestResourceStream("XianPal.SectHall.Background.png"))
            {Require(stream!=null,"production executable contains embedded hall art");using(Image image=Image.FromStream(stream))Require(image.Width>=320&&image.Height>=200,"production embedded PNG decodes");}
            Type hall=assembly.GetType("XianPal.SectHallControl",true);
            object instance=hall.GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic)[0].Invoke(new object[]{NullGetter(assembly.GetType("XianPal.CompanionSect",true)),NullGetter(assembly.GetType("XianPal.SectRun",true)),new Action<string>(delegate{})});
            using(Control control=(Control)instance)
            {
                Require((bool)hall.GetProperty("HasBackgroundImage",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(control,null),"installed hall loads actual background");
                string source=(string)hall.GetProperty("BackgroundSource",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(control,null);
                Require(source==args[0],"background source is "+args[0]+" with unrelated working directory");
                control.Size=new Size(960,640);using(var picture=new Bitmap(960,640)){control.DrawToBitmap(picture,new Rectangle(0,0,960,640));picture.Save(args[1]);}
            }
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
