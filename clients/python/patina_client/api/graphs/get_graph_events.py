from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.graph_event_page import GraphEventPage
from ...models.problem_details import ProblemDetails
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    id: str,
    *,
    since: int | Unset = 0,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["since"] = since


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/graphs/{id}/events".format(id=quote(str(id), safe=""),),
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> GraphEventPage | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = GraphEventPage.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[GraphEventPage | ProblemDetails]:
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
    since: int | Unset = 0,

) -> Response[GraphEventPage | ProblemDetails]:
    """ The events after a number. /ws/graphs/{id}?since= sends the same events as they happen.

    Args:
        id (str):
        since (int | Unset):  Default: 0.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphEventPage | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
since=since,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    since: int | Unset = 0,

) -> GraphEventPage | ProblemDetails | None:
    """ The events after a number. /ws/graphs/{id}?since= sends the same events as they happen.

    Args:
        id (str):
        since (int | Unset):  Default: 0.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphEventPage | ProblemDetails
     """


    return sync_detailed(
        id=id,
client=client,
since=since,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    since: int | Unset = 0,

) -> Response[GraphEventPage | ProblemDetails]:
    """ The events after a number. /ws/graphs/{id}?since= sends the same events as they happen.

    Args:
        id (str):
        since (int | Unset):  Default: 0.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphEventPage | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
since=since,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    since: int | Unset = 0,

) -> GraphEventPage | ProblemDetails | None:
    """ The events after a number. /ws/graphs/{id}?since= sends the same events as they happen.

    Args:
        id (str):
        since (int | Unset):  Default: 0.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphEventPage | ProblemDetails
     """


    return (await asyncio_detailed(
        id=id,
client=client,
since=since,

    )).parsed
