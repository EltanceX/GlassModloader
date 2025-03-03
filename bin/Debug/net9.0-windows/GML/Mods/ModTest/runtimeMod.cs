using Engine;
using System;
namespace GlassMod
{
    public class Mod
    {
        public static void GML_Main(string ver)
        {

            Console.WriteLine("Mod loaded!");
            if (Engine.Window.IsCreated)
            {

                Engine.Window.Title = $"SurvivalCraft {ver} [GML++]";
                Console.WriteLine("Title Chanded");
            }
        }
    }
}