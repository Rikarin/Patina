from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.graph_selection import GraphSelection
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    id: str,
    *,
    body: GraphSelection,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "put",
        "url": "/api/v1/graphs/{id}/selection".format(id=quote(str(id), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> GraphSelection | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = GraphSelection.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[GraphSelection | ProblemDetails]:
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
    body: GraphSelection,

) -> Response[GraphSelection | ProblemDetails]:
    """ Chooses nodes, and pins one or none, for every client. It is no edit, so undo never sees it. A
    client that follows the selection, as the viewport can, sees the change. Nodes the graph does not
    hold are left out.

    Args:
        id (str):
        body (GraphSelection): The nodes chosen in a graph session, and the one kept on view
            whatever is chosen. A session has one selection,
            whichever client made it last, so a client that follows it, such as the viewport, needs no
            editor (#244).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphSelection | ProblemDetails]
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
    body: GraphSelection,

) -> GraphSelection | ProblemDetails | None:
    """ Chooses nodes, and pins one or none, for every client. It is no edit, so undo never sees it. A
    client that follows the selection, as the viewport can, sees the change. Nodes the graph does not
    hold are left out.

    Args:
        id (str):
        body (GraphSelection): The nodes chosen in a graph session, and the one kept on view
            whatever is chosen. A session has one selection,
            whichever client made it last, so a client that follows it, such as the viewport, needs no
            editor (#244).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphSelection | ProblemDetails
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
    body: GraphSelection,

) -> Response[GraphSelection | ProblemDetails]:
    """ Chooses nodes, and pins one or none, for every client. It is no edit, so undo never sees it. A
    client that follows the selection, as the viewport can, sees the change. Nodes the graph does not
    hold are left out.

    Args:
        id (str):
        body (GraphSelection): The nodes chosen in a graph session, and the one kept on view
            whatever is chosen. A session has one selection,
            whichever client made it last, so a client that follows it, such as the viewport, needs no
            editor (#244).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[GraphSelection | ProblemDetails]
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
    body: GraphSelection,

) -> GraphSelection | ProblemDetails | None:
    """ Chooses nodes, and pins one or none, for every client. It is no edit, so undo never sees it. A
    client that follows the selection, as the viewport can, sees the change. Nodes the graph does not
    hold are left out.

    Args:
        id (str):
        body (GraphSelection): The nodes chosen in a graph session, and the one kept on view
            whatever is chosen. A session has one selection,
            whichever client made it last, so a client that follows it, such as the viewport, needs no
            editor (#244).

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        GraphSelection | ProblemDetails
     """


    return (await asyncio_detailed(
        id=id,
client=client,
body=body,

    )).parsed
