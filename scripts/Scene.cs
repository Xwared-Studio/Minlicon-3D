namespace Dov1ntc.MyGameIn3d;

using Unminal.Render.Objects;
using Unminal.UI.ButtonObject;
using Unminal.UI.Factory;
using Unminal.Utils.Colors;

[SupportedOSPlatform("windows")]
public class Scene {
    public static GameObject? node;
    public static GameObject? teapot;
    public static Button? btn1;

    public static List<GameObject> LoadScene(List<GameObject> _objects) {
        int mapSize = 40;
        float blockSize = 1f;
        float yLevel = -2f;
        Random rand = new Random();

        for (int x = 0; x < mapSize; x++)
        {
            for (int z = 0; z < mapSize; z++)
            {
                GameObject block = new GameObject(GetPath.GetCorrectPath("scripts/objects/cube.obj"));
                
                float posX = (x * blockSize) - (mapSize * blockSize) / 2f;
                float posZ = (z * blockSize) - (mapSize * blockSize) / 2f - 40f;
                
                block.Position = new Vector3(posX, yLevel, posZ);
                block.Scale = new Vector3(blockSize);
                
                float shade = 0.8f + (float)rand.NextDouble() * 0.2f;
                block.Color = new Vector3(0.2f * shade, 0.8f * shade, 0.2f * shade);
                
                _objects.Add(block);
            }
        }

        return _objects;
    }
}