using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class UIRootTests
{
    [UnityTest]
    public IEnumerator OverlappingRoots_RestorePreviousRootWhenNewestIsDestroyed()
    {
        GameObject firstObject = new GameObject("FirstUIRoot");
        UIRoot first = firstObject.AddComponent<UIRoot>();
        Assert.That(UIRoot.Active, Is.SameAs(first));

        LogAssert.Expect(LogType.Error, "More than one active UIRoot exists.");
        GameObject secondObject = new GameObject("SecondUIRoot");
        UIRoot second = secondObject.AddComponent<UIRoot>();
        Assert.That(UIRoot.Active, Is.SameAs(second));

        Object.Destroy(secondObject);
        yield return null;
        Assert.That(UIRoot.Active, Is.SameAs(first));

        Object.Destroy(firstObject);
        yield return null;
        Assert.That(UIRoot.Active, Is.Null);
    }
}
