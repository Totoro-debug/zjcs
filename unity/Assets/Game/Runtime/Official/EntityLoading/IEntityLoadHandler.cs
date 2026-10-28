// Original client/framework implementation; see entity loading source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Collections;

namespace Zjcs.Official.EntityLoading
{
public interface IEntityLoadHandler : IDisposable
{
	IEnumerator LoadForInit(Action<MyEntity> onLoadedEntity);

	void PreLoadGameObject(int entityClassId, Action<MyEntity> callback = null);

	void CreateMyEntity(ECViewEntityData entity, bool newEntity, Action<MyEntity> callback);

	void OnBeforeRemoveAllEntities();

	void DestroyMyEntity(MyEntity ent);
}

}
