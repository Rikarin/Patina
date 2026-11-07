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
    hash_: str,

) -> dict[str, Any]:
    

    

    

    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/previews/{hash_}".format(hash_=quote(str(hash_), safe=""),),
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> ProblemDetails | None:
    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    hash_: str,
    *,
    client: AuthenticatedClient | Client,

) -> Response[ProblemDetails]:
    """ A node's preview as a PNG, by the hash an event carried. It never changes, so cache it for ever.

    Args:
        hash_ (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails]
     """


    kwargs = _get_kwargs(
        hash_=hash_,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    hash_: str,
    *,
    client: AuthenticatedClient | Client,

) -> ProblemDetails | None:
    """ A node's preview as a PNG, by the hash an event carried. It never changes, so cache it for ever.

    Args:
        hash_ (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails
     """


    return sync_detailed(
        hash_=hash_,
client=client,

    ).parsed

async def asyncio_detailed(
    hash_: str,
    *,
    client: AuthenticatedClient | Client,

) -> Response[ProblemDetails]:
    """ A node's preview as a PNG, by the hash an event carried. It never changes, so cache it for ever.

    Args:
        hash_ (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails]
     """


    kwargs = _get_kwargs(
        hash_=hash_,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    hash_: str,
    *,
    client: AuthenticatedClient | Client,

) -> ProblemDetails | None:
    """ A node's preview as a PNG, by the hash an event carried. It never changes, so cache it for ever.

    Args:
        hash_ (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails
     """


    return (await asyncio_detailed(
        hash_=hash_,
client=client,

    )).parsed
