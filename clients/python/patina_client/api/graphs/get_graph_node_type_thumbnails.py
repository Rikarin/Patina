from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.thumbnail_list import ThumbnailList
from ...models.thumbnail_request import ThumbnailRequest
from typing import cast



def _get_kwargs(
    id: str,
    *,
    body: ThumbnailRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/graphs/{id}/node-types/thumbnails".format(id=quote(str(id), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | ThumbnailList | None:
    if response.status_code == 200:
        response_200 = ThumbnailList.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 503:
        response_503 = ProblemDetails.from_dict(response.json())



        return response_503

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | ThumbnailList]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: ThumbnailRequest,

) -> Response[ProblemDetails | ThumbnailList]:
    """ Pictures of node types this graph can use, each at its defaults with a noise on its picture inputs,
    by hash under /previews, drawing those the cache lacks first. A type with no picture has none.

    Args:
        id (str):
        body (ThumbnailRequest): The body of `POST /api/v1/library/thumbnails`. A thumbnail
            service for every client and every list: the
            Library's grid, the shelf, and anything after them.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ThumbnailList]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: ThumbnailRequest,

) -> ProblemDetails | ThumbnailList | None:
    """ Pictures of node types this graph can use, each at its defaults with a noise on its picture inputs,
    by hash under /previews, drawing those the cache lacks first. A type with no picture has none.

    Args:
        id (str):
        body (ThumbnailRequest): The body of `POST /api/v1/library/thumbnails`. A thumbnail
            service for every client and every list: the
            Library's grid, the shelf, and anything after them.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ThumbnailList
     """


    return sync_detailed(
        id=id,
client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: ThumbnailRequest,

) -> Response[ProblemDetails | ThumbnailList]:
    """ Pictures of node types this graph can use, each at its defaults with a noise on its picture inputs,
    by hash under /previews, drawing those the cache lacks first. A type with no picture has none.

    Args:
        id (str):
        body (ThumbnailRequest): The body of `POST /api/v1/library/thumbnails`. A thumbnail
            service for every client and every list: the
            Library's grid, the shelf, and anything after them.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ThumbnailList]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: ThumbnailRequest,

) -> ProblemDetails | ThumbnailList | None:
    """ Pictures of node types this graph can use, each at its defaults with a noise on its picture inputs,
    by hash under /previews, drawing those the cache lacks first. A type with no picture has none.

    Args:
        id (str):
        body (ThumbnailRequest): The body of `POST /api/v1/library/thumbnails`. A thumbnail
            service for every client and every list: the
            Library's grid, the shelf, and anything after them.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ThumbnailList
     """


    return (await asyncio_detailed(
        id=id,
client=client,
body=body,

    )).parsed
