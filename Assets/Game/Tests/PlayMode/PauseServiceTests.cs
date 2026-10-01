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

    [UnityTest]
    public IEnumerator DisablingService_ReleasesOwnedPause()
    {
        float originalTimeScale = Time.timeScale;
        GameObject host = new GameObject("PauseServiceDisableTests");
        PauseService pause = host.AddComponent<PauseService>();

        try
        {
            Time.timeScale = 0.75f;
            pause.SetPaused(this, true);
            Assert.That(Time.timeScale, Is.Zero);

            host.SetActive(false);
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
            Assert.That(pause.IsPaused, Is.False);
        }
        finally
        {
            Object.Destroy(host);
            Time.timeScale = originalTimeScale;
        }

        yield return null;
    }
}
