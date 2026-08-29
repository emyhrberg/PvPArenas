using Terraria.ModLoader;

namespace PvPArenas.Common.AdminTools.WorldGenManager;

public class NativeWorldGenLoaderSystem : ModSystem {
	public override void Load() {
		NativeLibraryLoader.Load(Mod);
	}

	public override void Unload() {
		NativeLibraryLoader.Unload();
	}
}
