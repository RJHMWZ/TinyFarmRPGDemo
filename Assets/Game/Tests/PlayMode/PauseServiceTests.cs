using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PauseServiceTests
{
    [UnityTest]
    public IEnumerator MultipleOwners_CannotResumeEachOther()
    {
        float originalTimeScale = Time.timeScale;
        GameObject host = new GameObject("PauseServiceTests");
        PauseService pause = host.AddComponent<PauseService>();
        var firstOwner = new object();
        var secondOwner = new object();

        try
        {
            Time.timeScale = 0.5f;
            pause.SetPaused(firstOwner, true);
            pause.SetPaused(secondOwner, true);
            Assert.That(Time.timeScale, Is.Zero);

            pause.SetPaused(firstOwner, false);
            Assert.That(pause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            pause.SetPaused(secondOwner, false);
            Assert.That(pause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
        }
        finally
        {
            Object.Destroy(host);
            Time.timeScale = originalTimeScale;
        }

        yield return null;
    }
}
