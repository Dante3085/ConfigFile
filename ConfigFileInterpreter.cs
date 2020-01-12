using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace ConfigFile
{
    public static class ConfigFileInterpreter
    {
        private static List<ConfigFile> configFileCache = new List<ConfigFile>();

        public static ConfigFile CheckConfigFileCache(String configFilePath)
        {
            ConfigFile configFile = configFileCache.Find(c => c.Path == configFilePath);
            if (configFile == null)
            {
                configFile = new ConfigFile(configFilePath);
                configFileCache.Add(configFile);
            }

            return configFile;
        }

        public static List<Animation> GetAnimations(String configFilePath)
        {
            ConfigFile file = CheckConfigFileCache(configFilePath);

            Category animCategory = file.GetCategory("Animation");
            List<Animation> animations = new List<Animation>();

            foreach (Section animSection in animCategory.Sections)
            {
                Animation anim = new Animation();
                anim.Name = (String)animSection.Attributes[0].Value;
                anim.Looped = (bool)animSection.Attributes[1].Value;
                anim.Mirrored = (bool)animSection.Attributes[2].Value;
                anim.Frames = (List<Rectangle>)animSection.Attributes[3].Value;
                anim.Offsets = (List<Vector2>)animSection.Attributes[4].Value;

                animations.Add(anim);
            }

            return animations;
        }
    }
}
