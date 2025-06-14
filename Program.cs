
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
namespace Fake_language_encoder
{
    public class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();
        private async Task MainAsync()
        {
            
            string test = "I remember as a child, and as a young budding naturalist, spending all my time observing and testing the world around me—moving pieces, " +
                "altering the flow of things, and documenting ways the world responded to me. Now, as an adult and a professional naturalist, I’ve approached language in the same way, " +
                "not from an academic point of view but as a curious child still building little mud dams in creeks and chasing after frogs. So this book is an odd thing: " +
                "it is a naturalist’s walk through the language-making landscape of the English language, and following in the naturalist’s tradition it combines " +
                "observation, experimentation, speculation, and documentation—activities we don’t normally associate with language";
            var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            var provider = new JsonDictionaryProvider(baseDir);
            var cipher = new TextCipher(Language.English, baseDir);
            string encrypted = await cipher.EncryptAsync(test, Language.English);
            string decrypted = await cipher.DecryptAsync(encrypted, Language.English);
            Console.WriteLine(test);
            Console.WriteLine("---");
            Console.WriteLine(encrypted);
            Console.WriteLine("---");
            Console.WriteLine(decrypted);
            
            

        }


    }
}