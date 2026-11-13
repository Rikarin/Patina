from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.stack_id_colours import StackIdColours
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    id: str,
    *,
    u: float | Unset = UNSET,
    v: float | Unset = UNSET,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["u"] = u

    params["v"] = v


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/stacks/{id}/id-colours".format(id=quote(str(id), safe=""),),
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | StackIdColours | None:
    if response.status_code == 200:
        response_200 = StackIdColours.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | StackIdColours]:
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
    u: float | Unset = UNSET,
    v: float | Unset = UNSET,

) -> Response[ProblemDetails | StackIdColours]:
    """ The colors of the ID map the stack reads, the largest first, and the one at a place on it if asked.

    Args:
        id (str):
        u (float | Unset):
        v (float | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackIdColours]
     """


    kwargs = _get_kwargs(
        id=id,
u=u,
v=v,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    u: float | Unset = UNSET,
    v: float | Unset = UNSET,

) -> ProblemDetails | StackIdColours | None:
    """ The colors of the ID map the stack reads, the largest first, and the one at a place on it if asked.

    Args:
        id (str):
        u (float | Unset):
        v (float | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackIdColours
     """


    return sync_detailed(
        id=id,
client=client,
u=u,
v=v,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    u: float | Unset = UNSET,
    v: float | Unset = UNSET,

) -> Response[ProblemDetails | StackIdColours]:
    """ The colors of the ID map the stack reads, the largest first, and the one at a place on it if asked.

    Args:
        id (str):
        u (float | Unset):
        v (float | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackIdColours]
     """


    kwargs = _get_kwargs(
        id=id,
u=u,
v=v,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    u: float | Unset = UNSET,
    v: float | Unset = UNSET,

) -> ProblemDetails | StackIdColours | None:
    """ The colors of the ID map the stack reads, the largest first, and the one at a place on it if asked.

    Args:
        id (str):
        u (float | Unset):
        v (float | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackIdColours
     """


    return (await asyncio_detailed(
        id=id,
client=client,
u=u,
v=v,

    )).parsed
