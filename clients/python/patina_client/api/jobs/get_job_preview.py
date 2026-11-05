from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    id: str,
    map_: str,

) -> dict[str, Any]:
    

    

    

    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/jobs/{id}/previews/{map_}".format(id=quote(str(id), safe=""),map_=quote(str(map_), safe=""),),
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Any | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = cast(Any, None)
        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[Any | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    map_: str,
    *,
    client: AuthenticatedClient | Client,

) -> Response[Any | ProblemDetails]:
    """ An 8-bit PNG of one map of a finished bake.

    Args:
        id (str):
        map_ (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[Any | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
map_=map_,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    map_: str,
    *,
    client: AuthenticatedClient | Client,

) -> Any | ProblemDetails | None:
    """ An 8-bit PNG of one map of a finished bake.

    Args:
        id (str):
        map_ (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Any | ProblemDetails
     """


    return sync_detailed(
        id=id,
map_=map_,
client=client,

    ).parsed

async def asyncio_detailed(
    id: str,
    map_: str,
    *,
    client: AuthenticatedClient | Client,

) -> Response[Any | ProblemDetails]:
    """ An 8-bit PNG of one map of a finished bake.

    Args:
        id (str):
        map_ (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[Any | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
map_=map_,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    map_: str,
    *,
    client: AuthenticatedClient | Client,

) -> Any | ProblemDetails | None:
    """ An 8-bit PNG of one map of a finished bake.

    Args:
        id (str):
        map_ (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Any | ProblemDetails
     """


    return (await asyncio_detailed(
        id=id,
map_=map_,
client=client,

    )).parsed
