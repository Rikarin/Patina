from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.stack_filter_kind import StackFilterKind
from typing import cast



def _get_kwargs(
    id: str,
    *,
    node: str,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["node"] = node


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/stacks/{id}/kind".format(id=quote(str(id), safe=""),),
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> ProblemDetails | StackFilterKind | None:
    if response.status_code == 200:
        response_200 = StackFilterKind.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[ProblemDetails | StackFilterKind]:
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
    node: str,

) -> Response[ProblemDetails | StackFilterKind]:
    """ What a node or compound this stack can run takes: its numbers, its settings and the images it reads,
    by port. The project's own compounds are included, which the vocabulary's lists are not.

    Args:
        id (str):
        node (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackFilterKind]
     """


    kwargs = _get_kwargs(
        id=id,
node=node,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    node: str,

) -> ProblemDetails | StackFilterKind | None:
    """ What a node or compound this stack can run takes: its numbers, its settings and the images it reads,
    by port. The project's own compounds are included, which the vocabulary's lists are not.

    Args:
        id (str):
        node (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackFilterKind
     """


    return sync_detailed(
        id=id,
client=client,
node=node,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    node: str,

) -> Response[ProblemDetails | StackFilterKind]:
    """ What a node or compound this stack can run takes: its numbers, its settings and the images it reads,
    by port. The project's own compounds are included, which the vocabulary's lists are not.

    Args:
        id (str):
        node (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackFilterKind]
     """


    kwargs = _get_kwargs(
        id=id,
node=node,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    node: str,

) -> ProblemDetails | StackFilterKind | None:
    """ What a node or compound this stack can run takes: its numbers, its settings and the images it reads,
    by port. The project's own compounds are included, which the vocabulary's lists are not.

    Args:
        id (str):
        node (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackFilterKind
     """


    return (await asyncio_detailed(
        id=id,
client=client,
node=node,

    )).parsed
