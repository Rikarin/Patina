from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.graph_node_matches import GraphNodeMatches
from ...models.problem_details import ProblemDetails
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    id: str,
    *,
    query: str | Unset = UNSET,
    takes: str | Unset = UNSET,
    gives: str | Unset = UNSET,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["query"] = query

    params["takes"] = takes

    params["gives"] = gives


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/graphs/{id}/node-types/search".format(id=quote(str(id), safe=""),),
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> GraphNodeMatches | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = GraphNodeMatches.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[GraphNodeMatches | ProblemDetails]:
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
    query: str | Unset = UNSET,
    takes: str | Unset = UNSET,
    gives: str | Unset = UNSET,

) -> Response[GraphNodeMatches | ProblemDetails]:
    """ The nodes this graph can use, best match first. takes and gives keep those a wire of that kind can
    use.

    Args:
        id (str):
        query (str | Unset):
        takes (str | Unset):
        gives (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphNodeMatches | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
query=query,
takes=takes,
gives=gives,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    query: str | Unset = UNSET,
    takes: str | Unset = UNSET,
    gives: str | Unset = UNSET,

) -> GraphNodeMatches | ProblemDetails | None:
    """ The nodes this graph can use, best match first. takes and gives keep those a wire of that kind can
    use.

    Args:
        id (str):
        query (str | Unset):
        takes (str | Unset):
        gives (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphNodeMatches | ProblemDetails
     """


    return sync_detailed(
        id=id,
client=client,
query=query,
takes=takes,
gives=gives,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    query: str | Unset = UNSET,
    takes: str | Unset = UNSET,
    gives: str | Unset = UNSET,

) -> Response[GraphNodeMatches | ProblemDetails]:
    """ The nodes this graph can use, best match first. takes and gives keep those a wire of that kind can
    use.

    Args:
        id (str):
        query (str | Unset):
        takes (str | Unset):
        gives (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphNodeMatches | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
query=query,
takes=takes,
gives=gives,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    query: str | Unset = UNSET,
    takes: str | Unset = UNSET,
    gives: str | Unset = UNSET,

) -> GraphNodeMatches | ProblemDetails | None:
    """ The nodes this graph can use, best match first. takes and gives keep those a wire of that kind can
    use.

    Args:
        id (str):
        query (str | Unset):
        takes (str | Unset):
        gives (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphNodeMatches | ProblemDetails
     """


    return (await asyncio_detailed(
        id=id,
client=client,
query=query,
takes=takes,
gives=gives,

    )).parsed
