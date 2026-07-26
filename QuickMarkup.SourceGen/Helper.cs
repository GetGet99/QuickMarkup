using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
namespace QuickMarkup.Infra;

static class APIExtension
{
    extension(ObjectDisposedException)
    {
        public static void ThrowIf([DoesNotReturnIf(true)] bool condition, object instance)
        {
            if (condition) throw new ObjectDisposedException(instance?.GetType().ToString());
        }
    }
}