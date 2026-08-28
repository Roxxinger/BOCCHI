/** Edge cache for public accepted-catalog GETs (coffers / carrots). */

export const CATALOG_CACHE_TTL_SECONDS = 300;

type CatalogKind = "coffers" | "carrots";

/** Stable synthetic URL — Cache API keys are Request URLs. */
function catalogCacheRequest(
  kind: CatalogKind,
  territoryId: string | null,
  dataId: string | null = null,
): Request {
  const url = new URL(`https://bocchi-catalog.internal/${kind}`);
  if (territoryId !== null) {
    url.searchParams.set("territoryId", territoryId);
  }

  if (dataId !== null) {
    url.searchParams.set("dataId", dataId);
  }

  return new Request(url.toString(), { method: "GET" });
}

export async function readCatalogCache(
  kind: CatalogKind,
  requestUrl: URL,
): Promise<Response | undefined> {
  const key = catalogCacheRequest(
    kind,
    requestUrl.searchParams.get("territoryId"),
    requestUrl.searchParams.get("dataId"),
  );
  return caches.default.match(key);
}

export async function writeCatalogCache(
  kind: CatalogKind,
  requestUrl: URL,
  response: Response,
): Promise<void> {
  const key = catalogCacheRequest(
    kind,
    requestUrl.searchParams.get("territoryId"),
    requestUrl.searchParams.get("dataId"),
  );
  await caches.default.put(key, response.clone());
}

/** Drop cached catalogs after accept/reject or clustering that may promote candidates. */
export async function invalidateAcceptedCatalogCaches(): Promise<void> {
  const territories: Array<string | null> = [null, "1252", "1346"];
  const deletes: Promise<boolean>[] = [];
  for (const territoryId of territories) {
    deletes.push(caches.default.delete(catalogCacheRequest("coffers", territoryId)));
    deletes.push(caches.default.delete(catalogCacheRequest("carrots", territoryId)));
  }

  await Promise.all(deletes);
}
