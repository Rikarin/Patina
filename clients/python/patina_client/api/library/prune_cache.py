from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.cache_prune_request import CachePruneRequest
from ...models.library_tidied import LibraryTidied
from ...models.problem_details import ProblemDetails
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    *,
    body: CachePruneRequest | None | Unset = UNSET,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/library/cache/prune",
    }

    
    if isinstance(body, CachePruneRequest):
        _kwargs["json"] = body.to_dict()
    else:
        _kwargs["json"] = body

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> LibraryTidied | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = LibraryTidied.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[LibraryTidied | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: CachePruneRequest | None | Unset = UNSET,

) -> Response[LibraryTidied | ProblemDetails]:
    """ Removes built objects, bakes and reports nothing has touched for a number of days, 30 by default. A
    later build makes them again. Refused while a bake or build is queued or running.

    Args:
        body (CachePruneRequest | None | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[LibraryTidied | ProblemDetails]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: CachePruneRequest | None | Unset = UNSET,

) -> LibraryTidied | ProblemDetails | None:
    """ Removes built objects, bakes and reports nothing has touched for a number of days, 30 by default. A
    later build makes them again. Refused while a bake or build is queued or running.

    Args:
        body (CachePruneRequest | None | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        LibraryTidied | ProblemDetails
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: CachePruneRequest | None | Unset = UNSET,

) -> Response[LibraryTidied | ProblemDetails]:
    """ Removes built objects, bakes and reports nothing has touched for a number of days, 30 by default. A
    later build makes them again. Refused while a bake or build is queued or running.

    Args:
        body (CachePruneRequest | None | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[LibraryTidied | ProblemDetails]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: CachePruneRequest | None | Unset = UNSET,

) -> LibraryTidied | ProblemDetails | None:
    """ Removes built objects, bakes and reports nothing has touched for a number of days, 30 by default. A
    later build makes them again. Refused while a bake or build is queued or running.

    Args:
        body (CachePruneRequest | None | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        LibraryTidied | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
